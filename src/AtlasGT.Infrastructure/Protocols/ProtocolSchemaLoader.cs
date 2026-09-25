using System.Text.Json;
using AtlasGT.Domain.Protocols;
using Microsoft.Extensions.Logging;
using AtlasGT.Infrastructure.Connectors;

namespace AtlasGT.Infrastructure.Protocols
{
    public class ProtocolSchemaLoader
    {
        private readonly ILogger<ProtocolSchemaLoader> _logger;

        public ProtocolSchemaLoader(ILogger<ProtocolSchemaLoader> logger)
        {
            _logger = logger;
        }

        public ProtocolSchema LoadFromJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var schema = JsonSerializer.Deserialize<ProtocolSchema>(json, options);
                
                if (schema == null) throw new Exception("Deserialization resulted in null.");
                
                _logger.LogInformation($"Successfully loaded protocol schema: {schema.ProtocolName}");
                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load protocol schema: {ex.Message}");
                throw;
            }
        }
    }
}
