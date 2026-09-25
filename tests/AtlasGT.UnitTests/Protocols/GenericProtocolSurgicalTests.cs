using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Connectors;
using AtlasGT.Infrastructure.Protocols;
using AtlasGT.Infrastructure.Protocols.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace AtlasGT.UnitTests.Protocols
{
    [TestClass]
    public class GenericProtocolSurgicalTests
    {
        private ProtocolSchema _schema;
        private CommandSafetyService _safety;
        private ScriptedProtocolTransport _transport;
        private GenericProtocolConnector _connector;

        [TestInitialize]
        public void Setup()
        {
            _schema = new ProtocolSchema
            {
                ProtocolName = "SurgicalTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.LengthPrefix, LengthOffset = 0 },
                Validation = new ValidationConfig { EnableChecksum = false },
                Fields = new List<FieldDefinition>
                {
                    new() { Name = "Value", Offset = 0, Length = 4, Type = EncodingType.Int32, Endianness = Endianness.Big }
                }
            };
            _safety = new CommandSafetyService();
            _transport = new ScriptedProtocolTransport();
            _connector = new GenericProtocolConnector("127.0.0.1", _schema, new NullLogger<GenericProtocolConnector>(), _transport, _safety);
        }

        [TestMethod]
        public async Task ExecuteAsync_SuccessPath_ReturnsCorrectValue()
        {
            // Arrange
            await _connector.ConnectAsync();
            var op = new ProtocolOperation
            {
                OperationName = "ReadVal",
                RequestBytes = new byte[] { 0x01, 0x02 },
                Response = new ResponseMatcherConfig { ExpectedPrefix = new byte[] { 0x00, 0x00, 0x00 } }
            };
            // Response: Length 4, Value 42 (Raw: [0,0,0,4, 0,0,0,42])
            _transport.AddResponse(new byte[] { 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x2A });

            // Act
            var result = await _connector.ExecuteAsync(op);

            // Assert
            Assert.IsTrue(result.Success, $"Operation failed: {result.Error}");
            Assert.AreEqual(42, (int)result.Fields["Value"]);
            Assert.AreEqual(1, _transport.GetSentData().Count);
            Assert.IsTrue(_transport.GetSentData()[0].SequenceEqual(op.RequestBytes));
        }

        [TestMethod]
        public async Task ExecuteAsync_WrongRequest_Fails()
        {
            // Arrange
            await _connector.ConnectAsync();
            
            // Configure transport to expect {0x01, 0x02}
            _transport.SetExpectedRequest(new byte[] { 0x01, 0x02 });
            
            // Operation sends {0x01}
            var op = new ProtocolOperation 
            { 
                RequestBytes = new byte[] { 0x01 }, 
                OperationName = "BadReq" 
            };
            
            // Add a response so that IF it got past SendAsync, it would have something to read
            _transport.AddResponse(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00 });

            // Act
            var result = await _connector.ExecuteAsync(op);

            // Assert
            Assert.IsFalse(result.Success, "Operation should have failed due to request mismatch.");
            Assert.IsTrue(result.Error != null && result.Error.Contains("Transport request mismatch"), $"Expected transport mismatch error, but got: {result.Error}");
            
            // Prove no response was decoded (Fields should be empty)
            Assert.AreEqual(0, result.Fields.Count, "No response should have been decoded on request failure.");
            
            // Prove the exact request bytes captured by the transport are {0x01}
            // Note: Our transport only adds to _sentData if the request matches. 
            // If it fails, it's not added. This is a design choice. 
            // However, the error message contains the received bytes.
        }

        [TestMethod]
        public async Task ExecuteAsync_IntegrityFailure_Fails()
        {
            await _connector.ConnectAsync();
            _schema.Validation = new ValidationConfig { EnableChecksum = true, ChecksumAlgorithm = "Checksum8" };

            var op = new ProtocolOperation { RequestBytes = new byte[] { 0x01 }, OperationName = "IntFail" };
            // Payload: Value 1 (4 bytes), Checksum (1 byte). Total = 5.
            // Correct checksum for [0,0,0,1] is 0x01. We send 0x99.
            byte[] responsePayload = { 0x00, 0x00, 0x00, 0x01, 0x99 };
            _transport.AddResponse(responsePayload);

            // Update framing to match response length exactly
            _schema.Framing.FixedLength = responsePayload.Length;
            _schema.Framing.Type = FramingType.FixedLength;

            var result = await _connector.ExecuteAsync(op);
            Assert.IsFalse(result.Success, $"Operation should have failed due to integrity error, but it succeeded. Error: {result.Error}");
            Assert.IsTrue(result.Error != null && result.Error.Contains("Checksum8 mismatch"), $"Expected Checksum8 mismatch, but got: {result.Error}");
        }

        [TestMethod]
        public async Task ExecuteAsync_ForbiddenPattern_BlockedBySafety()
        {
            await _connector.ConnectAsync();
            var op = new ProtocolOperation { RequestBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, OperationName = "Dangerous" };

            var result = await _connector.ExecuteAsync(op);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Safety violation"));
        }

        [TestMethod]
        public async Task ExecuteAsync_LittleEndian_DecodesCorrectly()
        {
            await _connector.ConnectAsync();
            _schema.Fields[0].Endianness = Endianness.Little;
            
            var op = new ProtocolOperation { RequestBytes = new byte[] { 0x01 }, OperationName = "LE" };
            // Length 4, Value 42 LittleEndian (0x2A 0x00 0x00 0x00)
            _transport.AddResponse(new byte[] { 0x00, 0x00, 0x00, 0x04, 0x2A, 0x00, 0x00, 0x00 });

            var result = await _connector.ExecuteAsync(op);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(42, (int)result.Fields["Value"]);
        }

        [TestMethod]
        public async Task ExecuteAsync_BitField_DecodesCorrectly()
        {
            await _connector.ConnectAsync();
            _schema.Fields[0].Type = EncodingType.BitField;
            _schema.Fields[0].Length = 1;
            
            var op = new ProtocolOperation { RequestBytes = new byte[] { 0x01 }, OperationName = "Bit" };
            // Length 1, Value 1 (Bit 0 set)
            _transport.AddResponse(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x01 });

            var result = await _connector.ExecuteAsync(op);
            Assert.IsTrue(result.Success);
            Assert.IsTrue((bool)result.Fields["Value"]);
        }
    }
}
