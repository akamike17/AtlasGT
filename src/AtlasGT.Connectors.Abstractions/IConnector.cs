using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Abstractions
{
    /// <summary>
    /// Represents the raw payload received from an endpoint, before any normalization.
    /// Carries provenance: where it came from and when it was received.
    /// </summary>
    public sealed class RawSample
    {
        public required string EndpointAddress { get; init; }
        public required DateTimeOffset ReceivedAtUtc { get; init; }
        public required ReadOnlyMemory<byte> Payload { get; init; }
        public required string TransportKind { get; init; } // "tcp", "udp", "serial", "mqtt", etc.
    }

    /// <summary>
    /// Connector lifecycle states. Connectors start in <see cref="Disconnected"/>.
    /// </summary>
    public enum ConnectorState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Faulted = 3,
        Disposed = 4
    }

    /// <summary>
    /// Read-only passive connector. Per spec section 2 (Principio de seguridad):
    /// "Una máquina desconocida jamás recibe comandos automáticamente."
    /// Writes/control are NOT in this interface — they require a separate capability.
    /// </summary>
    public interface IConnector : IAsyncDisposable
    {
        string EndpointAddress { get; }
        string TransportKind { get; }
        ConnectorState State { get; }

        /// <summary>
        /// Establishes the connection. Passive: does not send any data until authorized.
        /// </summary>
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads samples from the remote endpoint until the token is cancelled or the
        /// connection is closed. Passive: never writes.
        /// </summary>
        IAsyncEnumerable<RawSample> ReadAllAsync(CancellationToken cancellationToken = default);

        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Factory for connectors. Identified by a stable scheme ("tcp", "serial", "modbus-tcp", ...).
    /// </summary>
    public interface IConnectorFactory
    {
        string Scheme { get; }

        /// <summary>
        /// Returns true if this factory can build a connector for the given address.
        /// Example: "tcp://127.0.0.1:5000" is handled by the "tcp" factory.
        /// </summary>
        bool CanHandle(string address);

        IConnector Create(string address);
    }
}
