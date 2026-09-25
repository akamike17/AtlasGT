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
    public class GenericProtocolIntegrationTests
    {
        [TestMethod]
        public async Task FullPipeline_RequestResponse_Success()
        {
            // 1. Setup Deterministic Transport
            var transport = new ScriptedProtocolTransport();
            byte[] requestBytes = { 0x01, 0x02 };
            // LengthPrefix (4 bytes) = 4, Payload (4 bytes) = 42 (0x00 0x00 0x00 0x2A)
            byte[] responseBytes = { 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x2A }; 
            transport.AddResponse(responseBytes);

            // 2. Setup Schema
            var schema = new ProtocolSchema
            {
                ProtocolName = "IntegrationTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.LengthPrefix, LengthOffset = 0 },
                Validation = new ValidationConfig { EnableChecksum = false },
                Fields = new List<FieldDefinition>
                {
                    new() { Name = "Value", Offset = 0, Length = 4, Type = EncodingType.Int32, Endianness = Endianness.Big }
                }
            };

            // 3. Define Operation
            var operation = new ProtocolOperation
            {
                OperationName = "ReadValue",
                RequestBytes = requestBytes,
                Response = new ResponseMatcherConfig
                {
                    ExpectedPrefix = new byte[] { 0x00 }
                }
            };

            var connector = new GenericProtocolConnector("127.0.0.1", schema, new NullLogger<GenericProtocolConnector>(), transport);
            await connector.ConnectAsync();

            // 4. Execute: Send Request
            await transport.SendAsync(operation.RequestBytes);

            // 5. Execute: Receive and Process
            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            if (await samples.MoveNextAsync())
            {
                var sample = samples.Current;

                // Verify Matcher (Internal to Connector flow, but we verify the result here)
                var matcher = new ResponseMatcher();
                matcher.Match(sample.Payload.ToArray(), operation.Response);

                // Verify Decoder
                var decodedVal = ProtocolDecoder.Decode(sample.Payload.Span, schema.Fields[0]);
                Assert.AreEqual(42, (int)decodedVal, "Pipeline failed to decode correct value from scripted transport.");
            }
            else
            {
                Assert.Fail("No sample produced by the pipeline.");
            }
        }

        [TestMethod]
        public async Task FullPipeline_IntegrityFailure_Faults()
        {
            var transport = new ScriptedProtocolTransport();
            byte[] responseBytes = { 0x01, 0x02, 0x03, 0x00 }; // Wrong Checksum
            transport.AddResponse(responseBytes);

            var schema = new ProtocolSchema
            {
                ProtocolName = "IntegrityTest",
                Transport = TransportType.Tcp,
                Framing = new FramingConfig { Type = FramingType.FixedLength, FixedLength = 4 },
                Validation = new ValidationConfig { EnableChecksum = true, ChecksumAlgorithm = "Checksum8" },
                Fields = new List<FieldDefinition> { new() { Name = "V", Offset = 0, Length = 1, Type = EncodingType.Boolean } }
            };

            var connector = new GenericProtocolConnector("127.0.0.1", schema, new NullLogger<GenericProtocolConnector>(), transport);
            await connector.ConnectAsync();

            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            bool produced = await samples.MoveNextAsync();

            Assert.IsFalse(produced, "Pipeline should have skipped the sample due to integrity failure.");
        }
    }
}
