using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using AtlasGT.Infrastructure.Connectors;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

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
                Fields = new List<FieldDefinition>
                {
                    new() { Name = "Temp", Offset = 0, Length = 2, Type = EncodingType.Float32 } 
                }
            };
            // Float32 requires 4 bytes. The validator should check type-length consistency.
            // I will implement this check in the validator now.
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
                Framing = new FramingConfig { Type = FramingType.LengthPrefix, LengthOffset = 10 },
                Fields = new List<FieldDefinition> { new() { Name = "F1", Offset = 0, Length = 1, Type = EncodingType.Boolean } }
            };
            // LengthOffset cannot be greater than a reasonable limit or must be validated against frame
            Assert.ThrowsException<SchemaValidationException>(() => _// I will update validator to handle this
                _validator.Validate(schema));
        }
    }

    [TestClass]
    public class ProtocolRuntimeTests
    {
        [TestMethod]
        public async Task GenericProtocolRuntime_ScriptedTransport_RequestResponse_Decodes()
        {
            var logger = new NullLogger<ScriptedProtocolConnector>();
            var script = new ScriptedProtocol
            {
                ProtocolName = "TestScript",
                Transport = TransportType.Tcp,
                Steps = new List<ScriptedTransportStep>
                {
                    new() { StepName = "Query", RequestPayload = new byte[] { 0x01, 0x03 }, ExpectedResponseLength = 4, BlockUntilResponse = true }
                },
                ResponseSchema = new ProtocolSchema
                {
                    Fields = new List<FieldDefinition> { new() { Name = "Val", Offset = 0, Length = 4, Type = EncodingType.Int32 } }
                }
            };

            var connector = new ScriptedProtocolConnector("127.0.0.1", script, logger);
            await connector.ConnectAsync();
            
            var samples = connector.ReadAllAsync().GetAsyncEnumerator();
            if (await samples.MoveNextAsync())
            {
                var sample = samples.Current;
                Assert.IsNotNull(sample);
                Assert.AreEqual(4, sample.Payload.Length);
            }
            else
            {
                Assert.Fail("No sample produced by scripted transport.");
            }
        }
    }
}
