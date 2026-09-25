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
            
            return field.Type switch
            {
                EncodingType.Int32 => field.Endianness == Endianness.Big ? BinaryPrimitives.ReadInt32BigEndian(slice) : BinaryPrimitives.ReadInt32LittleEndian(slice),
                EncodingType.UInt32 => field.Endianness == Endianness.Big ? BinaryPrimitives.ReadUInt32BigEndian(slice) : BinaryPrimitives.ReadUInt32LittleEndian(slice),
                EncodingType.Float32 => field.Endianness == Endianness.Big ? BinaryPrimitives.ReadSingleBigEndian(slice) : BinaryPrimitives.ReadSingleLittleEndian(slice),
                EncodingType.Float64 => field.Endianness == Endianness.Big ? BinaryPrimitives.ReadDoubleBigEndian(slice) : BinaryPrimitives.ReadDoubleLittleEndian(slice),
                EncodingType.Boolean => slice[0] != 0,
                EncodingType.StringAscii => Encoding.ASCII.GetString(slice),
                _ => throw new NotSupportedException($"Encoding {field.Type} is not supported yet.")
            };
        }
    }
}
