namespace AtlasGT.Domain.Protocols
{
    public enum TransportType
    {
        Tcp,
        Udp,
        Serial,
        Http
    }

    public enum FramingType
    {
        FixedLength,
        Delimiter,
        LengthPrefix
    }

    public enum EncodingType
    {
        Int32,
        UInt32,
        Float32,
        Float64,
        StringAscii,
        Boolean,
        BitField
    }

    public enum Endianness
    {
        Big,
        Little
    }

    public sealed class ProtocolSchema
    {
        public string ProtocolName { get; set; } = string.Empty;
        public TransportType Transport { get; set; }
        public FramingConfig Framing { get; set; } = new();
        public ValidationConfig Validation { get; set; } = new();
        public List<FieldDefinition> Fields { get; set; } = new();
    }

    public sealed class FramingConfig
    {
        public FramingType Type { get; set; }
        public byte[]? Delimiter { get; set; }
        public int LengthOffset { get; set; }
        public int FixedLength { get; set; }
    }

    public sealed class ValidationConfig
    {
        public bool EnableChecksum { get; set; }
        public string ChecksumAlgorithm { get; set; } = "None"; // e.g., "CRC16", "Checksum8"
        public int ChecksumOffset { get; set; }
    }

    public sealed class FieldDefinition
    {
        public string Name { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public EncodingType Type { get; set; }
        public Endianness Endianness { get; set; } = Endianness.Big;
        public string? Unit { get; set; }
    }
}
