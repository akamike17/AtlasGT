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
        private readonly ILogger _logger;

        public ProtocolSchemaValidator(ILogger logger)
        {
            _logger = logger;
        }

        public void Validate(ProtocolSchema schema)
        {
            if (schema == null)
                throw new SchemaValidationException("Schema cannot be null.");

            if (string.IsNullOrWhiteSpace(schema.ProtocolName))
                throw new SchemaValidationException("Protocol name cannot be empty.");

            if (schema.Fields == null || schema.Fields.Count == 0)
                throw new SchemaValidationException("Protocol must define at least one field.");

            // 1. Type-Length Consistency Check
            foreach (var field in schema.Fields)
            {
                int requiredLength = field.Type switch
                {
                    EncodingType.Int32 or EncodingType.UInt32 or EncodingType.Float32 => 4,
                    EncodingType.Float64 => 8,
                    EncodingType.Boolean => 1,
                    EncodingType.BitField => 1, // Minimum 1 byte for a bitfield
                    _ => 0
                };

                if (requiredLength > 0 && field.Length < requiredLength)
                {
                    throw new SchemaValidationException($"Field {field.Name} of type {field.Type} requires at least {requiredLength} bytes, but {field.Length} was provided.");
                }
            }

            // 2. Check for overlapping fields and invalid offsets
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

            // 3. Framing Validation
            if (schema.Framing == null)
                throw new SchemaValidationException("Framing configuration is missing.");

            if (schema.Framing.Type == FramingType.FixedLength)
            {
                if (schema.Framing.FixedLength <= 0)
                    throw new SchemaValidationException("FixedLength framing requires a positive length.");
                
                if (currentPosition > schema.Framing.FixedLength)
                {
                    throw new SchemaValidationException($"Fields extend beyond the fixed frame length of {schema.Framing.FixedLength} bytes.");
                }
            }

            if (schema.Framing.Type == FramingType.LengthPrefix)
            {
                if (schema.Framing.LengthOffset < 0)
                    throw new SchemaValidationException("LengthPrefix offset cannot be negative.");
            }

            if (schema.Validation != null && schema.Validation.EnableChecksum)
            {
                var validAlgorithms = new[] { "None", "Checksum8", "CRC16" };
                if (!validAlgorithms.Contains(schema.Validation.ChecksumAlgorithm))
                {
                    throw new SchemaValidationException($"Unsupported checksum algorithm: {schema.Validation.ChecksumAlgorithm}. Supported: {string.Join(", ", validAlgorithms)}");
                }
            }

            _logger.LogInformation($"Protocol schema '{schema.ProtocolName}' validated successfully.");
        }
    }
}
