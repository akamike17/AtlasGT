using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using AtlasGT.Infrastructure.Connectors;
using AtlasGT.Infrastructure.Protocols.Testing;
using AtlasGT.Connectors.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace AtlasGT.UnitTests.Protocols
{
    [TestClass]
    public class FullPipelineTests
    {
        [TestMethod]
        public async Task Pipeline_DelimiterFraming_DecodesCorrectValue()
        {
            // 1. Setup Deterministic Transport
            var transport = new ScriptedTransport();
            byte[] expectedReq = { 0x01, 0x03 };
            // Payload: [0x00, 0x00, 0x00, 0x01, 0x03] -> Value 1, Delimiter 0x03
            byte[] scriptedRes = { 0x00, 0x00, 0x00, 0x01, 0x03 }; 
            transport.AddExchange(expectedReq, scriptedRes);

            // 2. Setup Schema with Delimiter
            var schema = new ProtocolSchema
            {
                ProtocolName = "DelimiterTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig 
                { 
                    Type = FramingType.Delimiter, 
                    Delimiter = new byte[] { 0x03 } 
                },
                Fields = new List<FieldDefinition> 
                { 
                    new() { Name = "Val", Offset = 0, Length = 4, Type = EncodingType.Int32, Endianness = Endianness.Big } 
                }
            };

            var script = new ScriptedProtocol
            {
                ProtocolName = "DelimiterScript",
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
                // The connector now uses FrameProcessor -> it should have stripped the 0x03
                Assert.AreEqual(4, sample.Payload.Length, "Frame processor should have stripped delimiter.");
                var decodedVal = ProtocolDecoder.Decode(sample.Payload.Span, schema.Fields[0]);
                Assert.AreEqual(1, (int)decodedVal);
            }
            else
            {
                Assert.Fail("No sample produced.");
            }
        }

        [TestMethod]
        public async Task Pipeline_IntegrityFailure_FaultsConnector()
        {
            var transport = new ScriptedTransport();
            byte[] expectedReq = { 0x01, 0x03 };
            // Payload: [0x00, 0x00, 0x00, 0x01, 0x99] -> Value 1, WRONG Checksum (0x99 instead of 0x01)
            byte[] scriptedRes = { 0x00, 0x00, 0x00, 0x01, 0x99 }; 
            transport.AddExchange(expectedReq, scriptedRes);

            var schema = new ProtocolSchema
            {
                ProtocolName = "ChecksumTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.FixedLength, FixedLength = 5 },
                Validation = new ValidationConfig { EnableChecksum = true, ChecksumAlgorithm = "Checksum8" },
                Fields = new List<FieldDefinition> 
                { 
                    new() { Name = "Val", Offset = 0, Length = 4, Type = EncodingType.Int32 } 
                }
            };

            var script = new ScriptedProtocol
            {
                ProtocolName = "CheckScript",
                Transport = TransportType.Tcp,
                Steps = new List<ScriptedTransportStep>
                {
                    new() { StepName = "Query", RequestPayload = expectedReq, BlockUntilResponse = true }
                },
                ResponseSchema = schema
            };

            var connector = new ScriptedProtocolConnector("127.0.0.1", script, new NullLogger<ScriptedProtocolConnector>(), transport);
            await connector.ConnectAsync();

            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            bool producedSample = await samples.MoveNextAsync();
            
            Assert.IsFalse(producedSample, "Connector should have faulted due to Integrity failure.");
            Assert.AreEqual(ConnectorState.Faulted, connector.State);
        }

        [TestMethod]
        public async Task Pipeline_LengthPrefix_DecodesCorrectValue()
        {
            var transport = new ScriptedTransport();
            byte[] expectedReq = { 0x01 };
            // Payload: [Header, Length=4, 0x00, 0x00, 0x00, 0x02]
            byte[] scriptedRes = { 0xAA, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02 }; 
            transport.AddExchange(expectedReq, scriptedRes);

            var schema = new ProtocolSchema
            {
                ProtocolName = "LengthPrefixTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.LengthPrefix, LengthOffset = 1 },
                Fields = new List<FieldDefinition> 
                { 
                    new() { Name = "Val", Offset = 0, Length = 4, Type = EncodingType.Int32 } 
                }
            };

            var script = new ScriptedProtocol
            {
                ProtocolName = "LengthScript",
                Transport = TransportType.Tcp,
                Steps = new List<ScriptedTransportStep>
                {
                    new() { StepName = "Query", RequestPayload = expectedReq, BlockUntilResponse = true }
                },
                ResponseSchema = schema
            };

            var connector = new ScriptedProtocolConnector("127.0.0.1", script, new NullLogger<ScriptedProtocolConnector>(), transport);
            await connector.ConnectAsync();

            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            if (await samples.MoveNextAsync())
            {
                var sample = samples.Current;
                Assert.AreEqual(4, sample.Payload.Length);
                var decodedVal = ProtocolDecoder.Decode(sample.Payload.Span, schema.Fields[0]);
                Assert.AreEqual(2, (int)decodedVal);
            }
            else
            {
                Assert.Fail("No sample produced.");
            }
        }
    }
}
