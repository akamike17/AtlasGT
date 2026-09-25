using AtlasGT.Domain.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AtlasGT.Infrastructure.Protocols
{
    public class FramingException : Exception
    {
        public FramingException(string message) : base(message) { }
    }

    public class FrameProcessor
    {
        public byte[] ExtractFrame(byte[] rawData, FramingConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            switch (config.Type)
            {
                case FramingType.FixedLength:
                    if (rawData.Length < config.FixedLength)
                        throw new FramingException($"Payload too short for FixedLength {config.FixedLength}. Actual: {rawData.Length}");
                    
                    return rawData.Take(config.FixedLength).ToArray();

                case FramingType.Delimiter:
                    if (config.Delimiter == null || config.Delimiter.Length == 0)
                        throw new FramingException("Delimiter not configured for Delimiter framing.");

                    int index = FindDelimiter(rawData, config.Delimiter);
                    if (index == -1)
                        throw new FramingException("Delimiter not found in payload.");

                    return rawData.Take(index).ToArray();

                case FramingType.LengthPrefix:
                    if (rawData.Length < config.LengthOffset + 4)
                        throw new FramingException("Payload too short to read length prefix.");

                    // Read length from prefix (assuming Int32 BigEndian for this implementation)
                    int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(rawData.AsSpan(config.LengthOffset, 4));

                    if (length < 0 || length > 10 * 1024 * 1024) // Safety limit: 10MB
                        throw new FramingException($"Declared length {length} is invalid or exceeds safety limits.");

                    if (rawData.Length < config.LengthOffset + 4 + length)
                        throw new FramingException($"Payload too short for declared length {length}.");

                    return rawData.AsSpan(config.LengthOffset + 4, length).ToArray();

                default:
                    throw new NotSupportedException($"Framing type {config.Type} is not supported.");
            }
        }

        private int FindDelimiter(byte[] data, byte[] delimiter)
        {
            for (int i = 0; i <= data.Length - delimiter.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < delimiter.Length; j++)
                {
                    if (data[i + j] != delimiter[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
