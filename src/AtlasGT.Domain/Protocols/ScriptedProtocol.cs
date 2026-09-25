namespace AtlasGT.Domain.Protocols
{
    public sealed class ScriptedTransportStep
    {
        public string StepName { get; set; } = string.Empty;
        public byte[]? RequestPayload { get; set; }
        public int ExpectedResponseLength { get; set; }
        public int TimeoutMs { get; set; } = 1000;
        public bool BlockUntilResponse { get; set; } = true;
    }

    public sealed class ScriptedProtocol
    {
        public string ProtocolName { get; set; } = string.Empty;
        public TransportType Transport { get; set; }
        public List<ScriptedTransportStep> Steps { get; set; } = new();
        public ProtocolSchema ResponseSchema { get; set; } = new();
    }
}
