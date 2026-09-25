using System.Collections.Generic;

namespace AtlasGT.Infrastructure.Protocols
{
    public sealed class DecodedResult
    {
        public Dictionary<string, object> Fields { get; set; } = new();
        public byte[] RawPayload { get; set; } = Array.Empty<byte>();
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
