using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Application;
using AtlasGT.Connectors.Network;
using AtlasGT.Connectors.Simulators;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;
using AtlasGT.Normalization;
using AtlasGT.Security;

namespace AtlasGT.EndToEndTests
{
    [TestClass]
    public class CountEndToEndTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (IOException) { /* archivo aun bloqueado por SO */ }
            catch (UnauthorizedAccessException) { /* permisos */ }
        }

        [TestMethod]
        public async Task Full_pipeline_persists_observations_and_promotes_trust()
        {
            // 1) Simulador
            var port = GetFreePort();
            var sim = new TcpSimulator(port);
            sim.Start();

            try
            {
                await Task.Delay(200);

                // 2) Endpoint en Passive
                var endpoint = new Endpoint
                {
                    Id = Guid.NewGuid(),
                    Name = $"tcp://127.0.0.1:{port}",
                    Address = $"tcp://127.0.0.1:{port}",
                    TrustTier = TrustTier.Passive
                };
                Assert.AreEqual(TrustTier.Passive, endpoint.TrustTier);

                // 3) Pipeline
                var historian = new FileObservationHistorian(_tempDir);
                var normalizer = new ObservationNormalizer();
                var ladder = new TrustLadder();
                var factory = new TcpConnectorFactory();
                var connector = factory.Create(endpoint.Address);

                var svc = new ObservationService(connector, normalizer, historian, ladder, endpoint);
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                var started = await svc.StartAsync(cts.Token);
                Assert.IsTrue(started, "El servicio de observacion debio arrancar");

                // 4) Dejar correr al menos 2 envios del simulador (sim emite cada 1s)
                await Task.Delay(TimeSpan.FromMilliseconds(2500), cts.Token);
                await svc.StopAsync();
                sim.Stop();

                // 5) Assertions
                Assert.IsTrue(svc.ObservationCount >= 1,
                    $"Se esperaban observaciones, hubo {svc.ObservationCount}");
                Assert.AreEqual(TrustTier.Observed, endpoint.TrustTier,
                    "El endpoint debio ser promovido a Observed");

                var day = DateTime.UtcNow.ToString("yyyyMMdd");
                var read = new System.Collections.Generic.List<Observation>();
                await foreach (var o in historian.ReadDayAsync(day))
                    read.Add(o);

                Assert.IsTrue(read.Count >= 1, "El historiador debio persistir al menos 1 observation");
                Assert.IsTrue(read.All(o => o.Name == "TEMP"), "Todas las obs deben ser TEMP (del simulador)");
                Assert.IsTrue(read.All(o => o.Value.HasValue && o.Value >= 15.0 && o.Value <= 35.0),
                    "Los valores deben estar en el rango del simulador");
            }
            finally
            {
                sim.Stop();
                sim.Dispose();
            }
        }

        private static int GetFreePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
