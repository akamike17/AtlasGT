using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AtlasGT.Infrastructure.Protocols
{
    public class ProtocolTestBed
    {
        private readonly ProtocolDecoder _decoder;
        private readonly ProtocolSchemaValidator _validator;
        private readonly ILogger<ProtocolTestBed> _logger;

        public ProtocolTestBed(ILogger<ProtocolTestBed> logger)
        {
            _logger = logger;
            _validator = new ProtocolSchemaValidator(logger);
        }

        public TestResult TestSchema(string jsonSchema, byte[] mockPayload)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var schema = JsonSerializer.Deserialize<ProtocolSchema>(jsonSchema, options);
                
                if (schema == null) return new TestResult { IsSuccess = false, Error = "Schema deserialization failed." };

                // 1. Validate Schema Structure
                _validator.Validate(schema);

                // 2. Attempt Decoding
                var decodedValues = new Dictionary<string, object>();
                foreach (var field in schema.Fields)
                {
                    var value = ProtocolDecoder.Decode(mockPayload, field);
                    decodedValues.Add(field.Name, value);
                }

                return new TestResult 
                { 
                    IsSuccess = true, 
                    DecodedData = decodedValues, 
                    Message = "Schema and payload are compatible." 
                };
            }
            catch (Exception ex)
            {
                return new TestResult { IsSuccess = false, Error = ex.Message };
            }
        }

        public sealed class TestResult
        {
            public bool IsSuccess { get; set; }
            public string? Error { get; set; }
            public string? Message { get; set; }
            public Dictionary<string, object>? DecodedData { get; set; }
        }
    }
}
