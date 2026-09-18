using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Network
{
    /// <summary>
    /// UDP listener pasivo. Escucha datagramas entrantes en un puerto.
    /// </summary>
    public sealed class UdpConnector : IConnector
    {
        private readonly int _port;
        private UdpClient? _udp;
        private ConnectorState _state = ConnectorState.Disconnected;

        public UdpConnector(int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            _port = port;
        }

        public string EndpointAddress => $"udp://0.0.0.0:{_port}";
        public string TransportKind => "udp";
        public ConnectorState State => _state;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectorState.Connected) return Task.FromResult(true);
            if (_state == ConnectorState.Disposed) return Task.FromResult(false);
            try
            {
                _udp = new UdpClient(_port);
                _state = ConnectorState.Connected;
                return Task.FromResult(true);
            }
            catch (SocketException)
            {
                _state = ConnectorState.Faulted;
                return Task.FromResult(false);
            }
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_udp is null) yield break;

            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { yield break; }
                catch (SocketException) { _state = ConnectorState.Faulted; yield break; }
                catch (ObjectDisposedException) { yield break; }

                yield return new RawSample
                {
                    EndpointAddress = $"udp://{result.RemoteEndPoint.Address}:{result.RemoteEndPoint.Port}",
                    ReceivedAtUtc = DateTimeOffset.UtcNow,
                    Payload = result.Buffer,
                    TransportKind = "udp"
                };
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try { _udp?.Close(); } catch (ObjectDisposedException) { /* ok */ }
            _udp = null;
            _state = ConnectorState.Disconnected;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _state = ConnectorState.Disposed;
        }
    }

    public sealed class UdpConnectorFactory : IConnectorFactory
    {
        public string Scheme => "udp";
        public bool CanHandle(string address) =>
            !string.IsNullOrWhiteSpace(address) && address.StartsWith("udp://", StringComparison.OrdinalIgnoreCase);

        public IConnector Create(string address)
        {
            if (!CanHandle(address)) throw new ArgumentException($"No se puede manejar '{address}'", nameof(address));
            var rest = address.Substring("udp://".Length);
            var idx = rest.LastIndexOf(':');
            if (idx < 0) throw new ArgumentException($"Formato esperado udp://host:port o udp://:port");
            var portStr = rest.Substring(idx + 1);
            if (!int.TryParse(portStr, out var port)) throw new ArgumentException($"Puerto invalido en '{address}'");
            return new UdpConnector(port);
        }
    }
}
