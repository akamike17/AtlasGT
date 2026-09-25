using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using AtlasGT.Infrastructure.Connectors;
using AtlasGT.Infrastructure.Protocols.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtlasGT.UnitTests.Protocols
{
    [TestClass]
    public class SchemaValidatorTests
    {
        private readonly ProtocolSchemaValidator _validator = new(new NullLogger<ProtocolSchemaValidator>());

        [TestMethod]
        public void SchemaValidator_RejectsFloat32Length2()
        {
            var schema = new ProtocolSchema
            {
                ProtocolName = "InvalidLength",
                Framing = new FramingConfig { Type = FramingType.FixedLength, FixedLength = 10 },
                Fields = new List<FieldDefinition>
                {
                    new() { Name = "Temp", Offset = 0, Length = 2, Type = EncodingType.Float32 } 
                }
            };
            Assert.ThrowsException<SchemaValidationException>(() => _validator.Validate(schema));
        }

        [TestMethod]
        public void SchemaValidator_RejectsInvalidFraming()
        {
            var schema = new ProtocolSchema
            {
                ProtocolName = "InvalidFraming",
                Framing = new FramingConfig { Type = FramingType.FixedLength, FixedLength = -1 },
                Fields = new List<FieldDefinition> { new() { Name = "F1", Offset = 0, Length = 1, Type = EncodingType.Boolean } }
            };
            Assert.ThrowsException<SchemaValidationException>(() => _validator.Validate(schema));
        }

        [TestMethod]
        public void SchemaValidator_RejectsInvalidLengthPrefix()
        {
            var schema = new ProtocolSchema
            {
                ProtocolName = "InvalidPrefix",
                Framing = new FramingConfig { Type = FramingType.LengthPrefix, LengthOffset = -1 },
                Fields = new List<FieldDefinition> { new() { Name = "F1", Offset = 0, Length = 1, Type = EncodingType.Boolean } }
            };
            Assert.ThrowsException<SchemaValidationException>(() => _validator.Validate(schema));
        }

        [TestMethod]
        public void SchemaValidator_RejectsNullFraming()
        {
            var schema = new ProtocolSchema
            {
                ProtocolName = "NullFraming",
                Framing = null!,
                Fields = new List<FieldDefinition> { new() { Name = "F1", Offset = 0, Length = 1, Type = EncodingType.Boolean } }
            };
            Assert.ThrowsException<SchemaValidationException>(() => _validator.Validate(schema));
        }
    }

    [TestClass]
    public class ProtocolRuntimeTests
    {
        [TestMethod]
        public async Task GoldenVector_RequestResponse_DecodesCorrectValue()
        {
            // 1. Setup Deterministic Transport
            var transport = new ScriptedTransport();
            byte[] expectedReq = { 0x01, 0x03 };
            byte[] scriptedRes = { 0x00, 0x00, 0x00, 0x01 }; // Int32 BigEndian = 1
            transport.AddExchange(expectedReq, scriptedRes);

            // 2. Setup Schema
            var schema = new ProtocolSchema
            {
                ProtocolName = "GoldenVector",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.FixedLength, FixedLength = 4 },
                Fields = new List<FieldDefinition> 
                { 
                    new() { Name = "Val", Offset = 0, Length = 4, Type = EncodingType.Int32, Endianness = Endianness.Big } 
                }
            };

            var script = new ScriptedProtocol
            {
                ProtocolName = "GoldenScript",
                Transport = TransportType.Tcp,
                Steps = new List<ScriptedTransportStep>
                {
                    new() { StepName = "Query", RequestPayload = expectedReq, BlockUntilResponse = true }
                },
                ResponseSchema = schema
            };

            var connector = new ScriptedProtocolConnector("127.0.0.1", script, new NullLogger<ScriptedProtocolConnector>(), transport);
            await connector.ConnectAsync();

            // 3. Execute Pipeline
            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            if (await samples.MoveNextAsync())
            {
                var sample = samples.Current;
                var decodedVal = ProtocolDecoder.Decode(sample.Payload, schema.Fields[0]);
                Assert.AreEqual(1, (int)decodedVal, "Golden vector failed: expected 1.");
            }
            else
            {
                Assert.Fail("No sample produced.");
            }
        }

        [TestMethod]
        public async Task NegativeTest_WrongRequest_FailsDeterministically()
        {
            var transport = new ScriptedTransport();
            transport.AddExchange(new byte[] { 0x01, 0x03 }, new byte[] { 0x00 });

            var script = new ScriptedProtocol
            {
                ProtocolName = "WrongReq",
                Transport = TransportType.Tcp,
                Steps = new List<ScriptedTransportStep>
                {
                    new() { StepName = "Query", RequestPayload = new byte[] { 0xFF, 0xFF }, BlockUntilResponse = true }
                }
            };

            var connector = new ScriptedProtocolConnector("127.0.0.1", script, new NullLogger<ScriptedProtocolConnector>(), transport);
            await connector.ConnectAsync();

            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            bool producedSample = await samples.MoveNextAsync();
            
            Assert.IsFalse(producedSample, "Connector should have faulted and produced no samples due to request mismatch.");
        }

        [TestMethod]
        public void LittleEndian_Regression_DecodesCorrectly()
        {
            // bytes: 01 00 00 00 -> UInt32 LittleEndian = 1
            byte[] payload = { 0x01, 0x00, 0x00, 0x00 };
            var field = new FieldDefinition { Offset = 0, Length = 4, Type = EncodingType.UInt32, Endianness = Endianness.Little };
            
            var result = (uint)ProtocolDecoder.Decode(payload, field);
            
            Assert.AreEqual(1u, result, "Little Endian regression failed: expected 1.");
        }
    }
}
