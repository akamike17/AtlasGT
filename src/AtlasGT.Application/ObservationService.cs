using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Connectors.Network;
using AtlasGT.Domain.Models;

namespace AtlasGT.Application
{
    /// <summary>
    /// Service that uses a TcpConnector to receive data and produce observations.
    /// </summary>
    public class ObservationService : IDisposable
    {
        private readonly TcpConnector _connector;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _readingTask;

        public ObservationService(string host, int port)
        {
            _connector = new TcpConnector(host, port);
        }

        public bool Start()
        {
            if (!_connector.Connect())
                return false;

            _readingTask = Task.Run(() => ReadLoop(_cts.Token));
            return true;
        }

        private async Task ReadLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    double? temperature = _connector.ReadTemperature(token);
                    if (temperature.HasValue)
                    {
                        // Create an observation
                        var observation = new Observation
                        {
                            Name = "Temperature",
                            Description = $"Temperature reading",
                            Value = temperature.Value,
                            Unit = "°C",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        Console.WriteLine($"Observation: {observation.Name} = {observation.Value}{observation.Unit} at {observation.CreatedAt}");
                    }
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in observation service: {ex.Message}");
            }
            finally
            {
                _connector.Disconnect();
            }
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _connector.Dispose();
        }
    }
}
