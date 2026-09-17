using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Application;
using AtlasGT.Connectors.Network;
using AtlasGT.Connectors.Simulators;

namespace AtlasGT.Count
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("AtlasGT.Count - Starting end-to-end demo with 3 simulated machines.");

            // We'll simulate three machines, each as a TCP simulator on a different port.
            var simulators = new List<TcpSimulator>();
            var connectors = new List<TcpConnector>();
            var observationServices = new List<ObservationService>();
            var cts = new CancellationTokenSource();

            try
            {
                // Start three simulators
                for (int i = 0; i < 3; i++)
                {
                    int port = 5000 + i;
                    var sim = new TcpSimulator(port);
                    sim.Start();
                    simulators.Add(sim);
                    Console.WriteLine($"Started TCP simulator on port {port}");
                }

                // Give simulators a moment to start
                await Task.Delay(500);

                // For each simulator, create a connector and an observation service
                for (int i = 0; i < 3; i++)
                {
                    int port = 5000 + i;
                    var connector = new TcpConnector("127.0.0.1", port);
                    if (connector.Connect())
                    {
                        connectors.Add(connector);
                        var obsService = new ObservationService("127.0.0.1", port);
                        if (obsService.Start())
                        {
                            observationServices.Add(obsService);
                            Console.WriteLine($"Connected and started observation service for machine {i+1} on port {port}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to start observation service for machine {i+1}");
                            connector.Disconnect();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to connect to simulator on port {port}");
                    }
                }

                // Let it run for 10 seconds
                Console.WriteLine("Running for 10 seconds...");
                await Task.Delay(10000, cts.Token);

                // Stop all observation services
                foreach (var obsService in observationServices)
                {
                    obsService.Stop();
                }
                // Stop all simulators
                foreach (var sim in simulators)
                {
                    sim.Stop();
                }
                // Dispose connectors
                foreach (var connector in connectors)
                {
                    connector.Dispose();
                }

                Console.WriteLine("Demo completed successfully.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Demo was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in demo: {ex.Message}");
            }
            finally
            {
                // Ensure cleanup
                foreach (var obsService in observationServices)
                {
                    obsService.Dispose();
                }
                foreach (var sim in simulators)
                {
                    sim.Dispose();
                }
                foreach (var connector in connectors)
                {
                    connector.Dispose();
                }
            }
        }
    }
}
