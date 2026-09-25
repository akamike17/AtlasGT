using System;
using System.Collections.Generic;

namespace AtlasGT.Domain.Protocols
{
    /// <summary>
    /// Represents a declarative operation to be performed by the Generic Protocol Connector.
    /// Defines the request to send and the criteria to match the response.
    /// </summary>
    public sealed class ProtocolOperation
    {
        public string OperationName { get; set; } = string.Empty;
        public byte[] RequestBytes { get; set; } = Array.Empty<byte>();
        public int TimeoutMs { get; set; } = 1000;
        public int RetryCount { get; set; } = 0;
        public int RetryDelayMs { get; set; } = 100;

        /// <summary>
        /// Defines how to match the received response to this operation.
        /// </summary>
        public ResponseMatcherConfig Response { get; set; } = new();
    }

    public sealed class ResponseMatcherConfig
    {
        /// <summary>
        /// If provided, the response must start with these bytes.
        /// </summary>
        public byte[]? ExpectedPrefix { get; set; }

        /// <summary>
        /// If provided, the response must exactly match these bytes.
        /// </summary>
        public byte[]? ExactMatch { get; set; }

        /// <summary>
        /// Overrides the global framing for this specific operation's response.
        /// </summary>
        public FramingConfig? FramingOverride { get; set; }

        /// <summary>
        /// Overrides the global validation for this specific operation's response.
        /// </summary>
        public ValidationConfig? ValidationOverride { get; set; }
    }
}
