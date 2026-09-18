using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Application;
using AtlasGT.Connectors.Network;
using AtlasGT.Connectors.Simulators;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;
using AtlasGT.Normalization;
using AtlasGT.Security;

namespace AtlasGT.Count
{
    /// <summary>
    /// Demo end-to-end: levanta 3 simuladores TCP y les conecta
    /// servicios de observacion con persistencia a disco.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("AtlasGT.Count - demo end-to-end: 3 maquinas simuladas -> observations -> JSONL.");

            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "count-demo");
            Directory.CreateDirectory(dataDir);

            var historian = new FileObservationHistorian(dataDir);
            var normalizer = new ObservationNormalizer();
            var ladder = new TrustLadder();
            var factory = new TcpConnectorFactory();

            var simulators = new List<TcpSimulator>();
            var services = new List<ObservationService>();
            var endpoints = new List<Endpoint>();
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                // 1) Levantar simuladores
                for (int i = 0; i < 3; i++)
                {
                    var port = 5000 + i;
                    var sim = new TcpSimulator(port);
                    sim.Start();
                    simulators.Add(sim);
                    Console.WriteLine($"[sim] simulador TCP escuchando en {port}");
                }

                // 2) Construir endpoints y servicios
                for (int i = 0; i < 3; i++)
                {
                    var address = $"tcp://127.0.0.1:{5000 + i}";
                    var endpoint = new Endpoint
                    {
                        Id = Guid.NewGuid(),
                        Name = address,
                        Address = address,
                        TrustTier = TrustTier.Passive
                    };
                    endpoints.Add(endpoint);

                    var connector = factory.Create(address);
                    var svc = new ObservationService(connector, normalizer, historian, ladder, endpoint);
                    svc.OnObservation += (_, obs) =>
                        Console.WriteLine($"[obs] {endpoint.Address} {obs.Name}={obs.Value}{obs.Unit} tier={endpoint.TrustTier}");
                    services.Add(svc);
                }

                // 3) Start
                foreach (var svc in services)
                    await svc.StartAsync(cts.Token);

                Console.WriteLine("Corriendo 5 segundos. Ctrl-C para salir antes.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); }
                catch (OperationCanceledException) { }

                // 4) Stop
                foreach (var svc in services) await svc.StopAsync();
                foreach (var sim in simulators) sim.Stop();

                // 5) Resumen
                var total = services.Sum(s => s.ObservationCount);
                Console.WriteLine($"Total de observaciones persistidas: {total}");
                Console.WriteLine($"TrustTier final por endpoint:");
                foreach (var ep in endpoints)
                    Console.WriteLine($"  {ep.Address} -> {ep.TrustTier}");
                Console.WriteLine($"Datos guardados en: {Path.GetFullPath(dataDir)}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
        }
    }
}
