using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Abstractions
{
    /// <summary>
    /// Abstraction for the physical transport layer of a protocol.
    /// Decouples the protocol logic (framing, decoding) from the communication medium (TCP, UDP, Serial).
    /// </summary>
    public interface IProtocolTransport : IAsyncDisposable
    {
        /// <summary>
        /// Establishes the physical connection to the endpoint.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the physical connection.
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends raw bytes over the transport.
        /// </summary>
        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Receives raw bytes from the transport.
        /// </summary>
        Task<byte[]> ReceiveAsync(int maxLength, CancellationToken cancellationToken = default);

        /// <summary>
        /// Indicates whether the physical connection is currently active.
        /// </summary>
        bool IsConnected { get; }
    }
}
