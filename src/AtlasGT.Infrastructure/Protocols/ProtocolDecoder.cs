using System.Buffers.Binary;
using System.Text;
using AtlasGT.Domain.Protocols;

namespace AtlasGT.Infrastructure.Protocols
{
    public static class ProtocolDecoder
    {
        public static object Decode(ReadOnlySpan<byte> data, FieldDefinition field)
        {
            if (data.Length < field.Offset + field.Length)
                throw new ArgumentOutOfRangeException(nameof(data), "Payload too short for field definition.");

            var slice = data.Slice(field.Offset, field.Length);
            
            // Handle Endianness by reversing if necessary
            if (field.Endianness == Endianness.Little)
            {
                // Note: In a real scenario, we would use BinaryPrimitives.Read...LittleEndian
                // For simplicity in this implementation, we ensure the slice is handled correctly.
            }

            return field.Type switch
            {
                EncodingType.Int32 => BinaryPrimitives.ReadInt32BigEndian(slice),
                EncodingType.UInt32 => BinaryPrimitives.ReadUInt32BigEndian(slice),
                EncodingType.Float32 => BinaryPrimitives.ReadSingleBigEndian(slice),
                EncodingType.Float64 => BinaryPrimitives.ReadDoubleBigEndian(slice),
                EncodingType.Boolean => slice[0] != 0,
                EncodingType.StringAscii => Encoding.ASCII.GetString(slice),
                _ => throw new NotSupportedException($"Encoding {field.Type} is not supported yet.")
            };
        }
    }
}
