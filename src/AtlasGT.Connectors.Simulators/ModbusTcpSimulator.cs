using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Simulators
{
    /// <summary>
    /// Simulador Modbus TCP: responde FC03/FC04 con registros configurables.
    /// MBAP: tid(2) pid(2) len(2) | luego len bytes = unit(1) + PDU.
    /// </summary>
    public sealed class ModbusTcpSimulator : IDisposable
    {
        private readonly int _port;
        private readonly ushort[] _registers;
        private TcpListener? _listener;
        private readonly CancellationTokenSource _cts = new();

        public ModbusTcpSimulator(int port, ushort[]? registers = null)
        {
            _port = port;
            _registers = registers ?? new ushort[] { 73, 1, 18452 % 65536, 42 };
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }

        private async Task ListenLoop(CancellationToken token)
        {
            if (_listener is null) return;
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                _ = Task.Run(() => HandleAsync(client, token));
            }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var mbap = new byte[6]; // tid(2) pid(2) len(2)
                    while (!token.IsCancellationRequested)
                    {
                        if (!await ReadExact(stream, mbap, 6, token).ConfigureAwait(false)) break;
                        var len = (mbap[4] << 8) | mbap[5];
                        if (len < 2 || len > 260) break; // sanity: unit(1)+pdu(>=1)
                        var unitAndPdu = new byte[len];
                        if (!await ReadExact(stream, unitAndPdu, len, token).ConfigureAwait(false)) break;

                        var unit = unitAndPdu[0];
                        var fc = unitAndPdu[1];
                        if (fc != 3 && fc != 4) continue;

                        var start = (unitAndPdu[2] << 8) | unitAndPdu[3];
                        var qty = (unitAndPdu[4] << 8) | unitAndPdu[5];
                        var byteCount = qty * 2;

                        var respPdu = new byte[2 + byteCount];
                        respPdu[0] = fc;
                        respPdu[1] = (byte)byteCount;
                        for (int i = 0; i < qty; i++)
                        {
                            var v = (start + i) < _registers.Length ? _registers[start + i] : (ushort)0;
                            respPdu[2 + i * 2] = (byte)(v >> 8);
                            respPdu[2 + i * 2 + 1] = (byte)(v & 0xFF);
                        }

                        // respuesta: tid(2) pid(2) len(2) unit(1) pdu
                        var resp = new byte[6 + 1 + respPdu.Length];
                        resp[0] = mbap[0]; resp[1] = mbap[1];
                        resp[2] = mbap[2]; resp[3] = mbap[3];
                        var rl = (ushort)(1 + respPdu.Length);
                        resp[4] = (byte)(rl >> 8); resp[5] = (byte)(rl & 0xFF);
                        resp[6] = unit;
                        Array.Copy(respPdu, 0, resp, 7, respPdu.Length);

                        await stream.WriteAsync(resp, 0, resp.Length, token).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private static async Task<bool> ReadExact(NetworkStream s, byte[] buf, int count, CancellationToken ct)
        {
            var read = 0;
            while (read < count)
            {
                int n;
                try { n = await s.ReadAsync(buf, read, count - read, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (IOException) { return false; }
                if (n == 0) return false;
                read += n;
            }
            return true;
        }

        public void Stop() { _cts.Cancel(); try { _listener?.Stop(); } catch (ObjectDisposedException) { } }
        public void Dispose() { Stop(); _cts.Dispose(); }
    }
}
