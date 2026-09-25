using AtlasGT.Domain.Protocols;
using Microsoft.Extensions.Logging;

namespace AtlasGT.Infrastructure.Protocols
{
    public class SchemaValidationException : Exception
    {
        public SchemaValidationException(string message) : base(message) { }
    }

    public class ProtocolSchemaValidator
    {
        private readonly ILogger<ProtocolSchemaValidator> _logger;

        public ProtocolSchemaValidator(ILogger<ProtocolSchemaValidator> logger)
        {
            _logger = logger;
        }

        public void Validate(ProtocolSchema schema)
        {
            if (string.IsNullOrWhiteSpace(schema.ProtocolName))
                throw new SchemaValidationException("Protocol name cannot be empty.");

            if (schema.Fields == null || schema.Fields.Count == 0)
                throw new SchemaValidationException("Protocol must define at least one field.");

            // Check for overlapping fields and invalid offsets
            var sortedFields = schema.Fields.OrderBy(f => f.Offset).ToList();
            int currentPosition = 0;

            foreach (var field in sortedFields)
            {
                if (field.Offset < 0)
                    throw new SchemaValidationException($"Field {field.Name} has negative offset.");

                if (field.Length <= 0)
                    throw new SchemaValidationException($"Field {field.Name} must have a positive length.");

                if (field.Offset < currentPosition)
                {
                    throw new SchemaValidationException($"Field {field.Name} overlaps with previous fields at offset {field.Offset}.");
                }

                currentPosition = field.Offset + field.Length;
            }

            // Validate Framing vs Fields
            if (schema.Framing.Type == FramingType.FixedLength && schema.Framing.FixedLength > 0)
            {
                if (currentPosition > schema.Framing.FixedLength)
                {
                    throw new SchemaValidationException($"Fields extend beyond the fixed frame length of {schema.Framing.FixedLength} bytes.");
                }
            }

            _logger.LogInformation($"Protocol schema '{schema.ProtocolName}' validated successfully.");
        }
    }
}
