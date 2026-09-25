namespace AtlasGT.Infrastructure.Protocols
{
    public sealed class CommandDryRunResult
    {
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public byte[]? PredictedPayload { get; set; }
        public string? Warning { get; set; }
    }

    public interface ICommandSafetyService
    {
        CommandDryRunResult VerifyCommand(ProtocolSchema schema, byte[] commandPayload);
        bool IsWriteEnabled(string endpointAddress);
        void EnableWrite(string endpointAddress);
    }

    public class CommandSafetyService : ICommandSafetyService
    {
        private readonly HashSet<string> _enabledEndpoints = new();

        public CommandDryRunResult VerifyCommand(ProtocolSchema schema, byte[] commandPayload)
        {
            // 1. Check for forbidden patterns (e.g., "Reset All" commands)
            // 2. Validate against schema length/framing
            if (schema.Framing.Type == FramingType.FixedLength && commandPayload.Length != schema.Framing.FixedLength)
            {
                return new CommandDryRunResult { IsValid = false, Error = "Payload length mismatch for fixed-length protocol." };
            }

            return new CommandDryRunResult 
            { 
                IsValid = true, 
                PredictedPayload = commandPayload, 
                Warning = "Command verified against schema. Ensure machine is in standby." 
            };
        }

        public bool IsWriteEnabled(string endpointAddress) => _enabledEndpoints.Contains(endpointAddress);

        public void EnableWrite(string endpointAddress) => _enabledEndpoints.Add(endpointAddress);
    }
}
