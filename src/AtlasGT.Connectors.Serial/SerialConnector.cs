using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Serial
{
    /// <summary>
    /// Conector serial pasivo. Solo lee bytes del puerto configurado.
    /// </summary>
    public sealed class SerialConnector : IConnector
    {
        private readonly string _portName;
        private readonly int _baud;
        private SerialPort? _port;
        private ConnectorState _state = ConnectorState.Disconnected;

        public SerialConnector(string portName, int baud = 9600)
        {
            if (string.IsNullOrWhiteSpace(portName)) throw new ArgumentException("portName requerido", nameof(portName));
            if (baud <= 0) throw new ArgumentOutOfRangeException(nameof(baud));
            _portName = portName;
            _baud = baud;
        }

        public string EndpointAddress => $"serial://{_portName}?baud={_baud}";
        public string TransportKind => "serial";
        public ConnectorState State => _state;

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectorState.Connected) return Task.FromResult(true);
            try
            {
                _port = new SerialPort(_portName, _baud)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    NewLine = "\n"
                };
                _port.Open();
                _state = ConnectorState.Connected;
                return Task.FromResult(true);
            }
            catch (UnauthorizedAccessException) { _state = ConnectorState.Faulted; return Task.FromResult(false); }
            catch (IOException) { _state = ConnectorState.Faulted; return Task.FromResult(false); }
            catch (ArgumentException) { _state = ConnectorState.Faulted; return Task.FromResult(false); }
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_port is null || !_port.IsOpen) yield break;
            var sb = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested && _port.IsOpen)
            {
                int b;
                try
                {
                    b = _port.ReadByte();
                }
                catch (TimeoutException) { await Task.Delay(10, cancellationToken).ConfigureAwait(false); continue; }
                catch (OperationCanceledException) { yield break; }
                catch (IOException) { _state = ConnectorState.Faulted; yield break; }

                if (b == -1) { await Task.Delay(10, cancellationToken).ConfigureAwait(false); continue; }

                if (b == '\n')
                {
                    if (sb.Length == 0) continue;
                    var line = sb.ToString();
                    sb.Clear();
                    yield return new RawSample
                    {
                        EndpointAddress = EndpointAddress,
                        ReceivedAtUtc = DateTimeOffset.UtcNow,
                        Payload = Encoding.ASCII.GetBytes(line),
                        TransportKind = "serial"
                    };
                }
                else if (b != '\r')
                {
                    sb.Append((char)b);
                }
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_port is not null && _port.IsOpen) _port.Close();
            }
            catch (IOException) { /* cable desconectado etc */ }
            _port = null;
            _state = ConnectorState.Disconnected;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _state = ConnectorState.Disposed;
        }
    }

    public sealed class SerialConnectorFactory : IConnectorFactory
    {
        public string Scheme => "serial";

        public bool CanHandle(string address) =>
            !string.IsNullOrWhiteSpace(address) && address.StartsWith("serial://", StringComparison.OrdinalIgnoreCase);

        public IConnector Create(string address)
        {
            // serial://COM3?baud=9600
            var rest = address.Substring("serial://".Length);
            var q = rest.IndexOf('?');
            var port = q >= 0 ? rest.Substring(0, q) : rest;
            var baud = 9600;
            if (q >= 0)
            {
                var qs = rest.Substring(q + 1);
                foreach (var kv in qs.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = kv.Split('=', 2);
                    if (parts.Length == 2 && parts[0] == "baud" && int.TryParse(parts[1], out var b))
                        baud = b;
                }
            }
            return new SerialConnector(port, baud);
        }
    }
}
