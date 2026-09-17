using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Simulators
{
    /// <summary>
    /// A simple TCP simulator that listens for connections and sends random data.
    /// </summary>
    public class TcpSimulator : IDisposable
    {
        private readonly int _port;
        private TcpListener _listener;
        private bool _isRunning;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public TcpSimulator(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;
            _ = Task.Run(() => ListenLoop(_cts.Token));
        }

        private async Task ListenLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, token));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Log or handle exception
                Console.WriteLine($"Listener error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            try
            {
                var stream = client.GetStream();
                var random = new Random();
                while (!token.IsCancellationRequested && client.Connected)
                {
                    // Generate some random data (e.g., a temperature reading)
                    double temperature = 20.0 + random.NextDouble() * 10.0; // 20-30°C
                    string message = $"TEMP:{temperature:F1}\n";
                    byte[] buffer = Encoding.ASCII.GetBytes(message);
                    await stream.WriteAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    await Task.Delay(1000, token).ConfigureAwait(false); // send every second
                }
            }
            catch (Exception ex)
            {
                // Client disconnected or error
                Console.WriteLine($"Client handler error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener?.Stop();
            _isRunning = false;
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}
