using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Network
{
    /// <summary>
    /// Conector TCP pasivo. Solo lee; jamas escribe al endpoint.
    /// La lectura es por lineas ASCII terminadas en '\n'.
    /// </summary>
    public class TcpConnector : IConnector
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private ConnectorState _state = ConnectorState.Disconnected;

        public TcpConnector(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host requerido", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            _host = host;
            _port = port;
        }

        public string EndpointAddress => $"tcp://{_host}:{_port}";
        public string TransportKind => "tcp";
        public ConnectorState State => _state;

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectorState.Connected) return true;
            if (_state == ConnectorState.Disposed) return false;

            _state = ConnectorState.Connecting;
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
                _stream = _client.GetStream();
                _state = ConnectorState.Connected;
                return true;
            }
            catch (OperationCanceledException)
            {
                _state = ConnectorState.Disconnected;
                return false;
            }
            catch (SocketException)
            {
                _state = ConnectorState.Faulted;
                CleanupClient();
                return false;
            }
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_state != ConnectorState.Connected || _stream is null)
                yield break;

            var buffer = new byte[1];
            var lineBuffer = new MemoryStream();

            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                int read;
                int b;
                try
                {
                    read = _stream.DataAvailable ? 1 : 0;
                    if (read == 0)
                    {
                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    b = _stream.ReadByte();
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (Exception)
                {
                    _state = ConnectorState.Faulted;
                    yield break;
                }

                if (b == -1)
                {
                    _state = ConnectorState.Disconnected;
                    yield break;
                }

                if (b == '\n')
                {
                    if (lineBuffer.Length == 0) continue;
                    var payload = lineBuffer.ToArray();
                    lineBuffer.SetLength(0);
                    yield return new RawSample
                    {
                        EndpointAddress = EndpointAddress,
                        ReceivedAtUtc = DateTimeOffset.UtcNow,
                        Payload = payload,
                        TransportKind = TransportKind
                    };
                }
                else if (b != '\r')
                {
                    lineBuffer.WriteByte((byte)b);
                }
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            CleanupClient();
            _state = ConnectorState.Disconnected;
            return Task.CompletedTask;
        }

        private void CleanupClient()
        {
            try { _stream?.Dispose(); } catch (ObjectDisposedException) { /* ya dispuesto */ }
            try { _client?.Close(); } catch (ObjectDisposedException) { /* ya cerrado */ } catch (SocketException) { /* ya cerrado */ }
            _stream = null;
            _client = null;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _state = ConnectorState.Disposed;
        }
    }
}
