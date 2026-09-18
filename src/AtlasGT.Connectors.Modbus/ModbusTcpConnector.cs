using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Abstractions;

namespace AtlasGT.Connectors.Modbus
{
    /// <summary>
    /// Cliente Modbus TCP minimo pero real: lee holding registers (FC03)
    /// e input registers (FC04). Pasivo: no escribe (WriteMultiple/WriteSingle
    /// NO implementados por disenio — read-only por defecto).
    /// </summary>
    public sealed class ModbusTcpConnector : IConnector
    {
        private readonly string _host;
        private readonly int _port;
        private readonly byte _unitId;
        private readonly IReadOnlyList<ModbusReadRequest> _requests;
        private readonly TimeSpan _pollInterval;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private ConnectorState _state = ConnectorState.Disconnected;
        private ushort _transactionId;

        public ModbusTcpConnector(
            string host,
            int port,
            byte unitId,
            IReadOnlyList<ModbusReadRequest> requests,
            TimeSpan? pollInterval = null)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host requerido", nameof(host));
            _host = host;
            _port = port;
            _unitId = unitId;
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
            if (_requests.Count == 0) throw new ArgumentException("Se requiere al menos un request", nameof(requests));
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        }

        public string EndpointAddress => $"modbus-tcp://{_host}:{_port}/{_unitId}";
        public string TransportKind => "modbus-tcp";
        public ConnectorState State => _state;

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_state == ConnectorState.Connected) return true;
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
                _stream = _client.GetStream();
                _state = ConnectorState.Connected;
                return true;
            }
            catch (SocketException) { _state = ConnectorState.Faulted; return false; }
            catch (OperationCanceledException) { _state = ConnectorState.Disconnected; return false; }
        }

        public async IAsyncEnumerable<RawSample> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested && _state == ConnectorState.Connected)
            {
                foreach (var req in _requests)
                {
                    byte[]? frame = null;
                    try
                    {
                        using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        reqCts.CancelAfter(TimeSpan.FromSeconds(3));
                        frame = await ReadRegistersAsync(req, reqCts.Token).ConfigureAwait(false);
                    }
                    catch (SocketException) { _state = ConnectorState.Faulted; }
                    catch (IOException) { _state = ConnectorState.Faulted; }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // timeout de una lectura individual: continuar
                    }
                    catch (OperationCanceledException) { yield break; }

                    if (frame is not null && frame.Length > 0)
                    {
                        yield return new RawSample
                        {
                            EndpointAddress = EndpointAddress,
                            ReceivedAtUtc = DateTimeOffset.UtcNow,
                            Payload = frame,
                            TransportKind = $"modbus-tcp/{req.FunctionCode}"
                        };
                    }

                    if (_state != ConnectorState.Connected) yield break;
                }

                try { await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
        }

        /// <summary>
        /// Ejecuta una lectura FC03/FC04 y devuelve el ADU completo (MBAP + PDU) como payload.
        /// </summary>
        private async Task<byte[]> ReadRegistersAsync(ModbusReadRequest req, CancellationToken ct)
        {
            if (_stream is null) throw new InvalidOperationException("No conectado");
            var tid = ++_transactionId;

            // MBAP: tid(2) pid(2=0) len(2) unit(1) | PDU: fc(1) start(2) qty(2)
            var pdu = new byte[5];
            pdu[0] = req.FunctionCode;
            pdu[1] = (byte)(req.StartAddress >> 8);
            pdu[2] = (byte)(req.StartAddress & 0xFF);
            pdu[3] = (byte)(req.Quantity >> 8);
            pdu[4] = (byte)(req.Quantity & 0xFF);

            var adu = new byte[7 + pdu.Length];
            adu[0] = (byte)(tid >> 8); adu[1] = (byte)(tid & 0xFF);
            adu[2] = 0; adu[3] = 0;
            var len = (ushort)(1 + pdu.Length);
            adu[4] = (byte)(len >> 8); adu[5] = (byte)(len & 0xFF);
            adu[6] = _unitId;
            Array.Copy(pdu, 0, adu, 7, pdu.Length);

            await _stream.WriteAsync(adu, 0, adu.Length, ct).ConfigureAwait(false);

            // Leer respuesta: MBAP(6) = tid(2) pid(2) len(2); luego len bytes = unit(1) + PDU
            var mbap = await ReadExactAsync(6, ct).ConfigureAwait(false);
            if (mbap is null) throw new IOException("Conexion cerrada durante MBAP");
            var respLen = (mbap[4] << 8) | mbap[5];
            var body = await ReadExactAsync(respLen, ct).ConfigureAwait(false);
            if (body is null) throw new IOException("Conexion cerrada durante body");

            var full = new byte[6 + respLen];
            Array.Copy(mbap, 0, full, 0, 6);
            Array.Copy(body, 0, full, 6, respLen);
            return full;
        }

        private async Task<byte[]?> ReadExactAsync(int count, CancellationToken ct)
        {
            if (_stream is null) return null;
            var buf = new byte[count];
            var read = 0;
            while (read < count)
            {
                int n;
                try { n = await _stream.ReadAsync(buf, read, count - read, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (IOException) { return null; }
                if (n == 0) return null;
                read += n;
            }
            return buf;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try { _stream?.Dispose(); } catch (ObjectDisposedException) { }
            try { _client?.Close(); } catch (ObjectDisposedException) { }
            _stream = null; _client = null;
            _state = ConnectorState.Disconnected;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            _state = ConnectorState.Disposed;
        }
    }

    public sealed class ModbusReadRequest
    {
        public byte FunctionCode { get; init; } = 3; // 3 = holding, 4 = input
        public ushort StartAddress { get; init; }
        public ushort Quantity { get; init; }
    }

    /// <summary>
    /// Decodificador de respuestas Modbus (FC03/FC04) a registros crudos.
    /// </summary>
    public static class ModbusResponseDecoder
    {
        /// <summary>Decodifica ADU a arreglo de registros ushort, o null si invalida.</summary>
        public static ushort[]? DecodeRegisters(byte[] adu)
        {
            if (adu is null || adu.Length < 9) return null;
            var fc = adu[7];
            if (fc != 3 && fc != 4) return null;
            var byteCount = adu[8];
            if (adu.Length != 9 + byteCount) return null;
            var regs = new ushort[byteCount / 2];
            for (int i = 0; i < regs.Length; i++)
                regs[i] = (ushort)((adu[9 + i * 2] << 8) | adu[9 + i * 2 + 1]);
            return regs;
        }
    }
}
