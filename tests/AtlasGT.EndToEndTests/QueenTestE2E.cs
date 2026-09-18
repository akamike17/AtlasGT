using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Application;
using AtlasGT.Connectors.Simulators;
using AtlasGT.Connectors.Network;
using AtlasGT.Discovery;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;
using AtlasGT.Normalization;
using AtlasGT.Security;

namespace AtlasGT.EndToEndTests
{
    /// <summary>
    /// Prueba reina (spec sec. 29) simplificada pero real:
    /// simulador mudo -> discovery -> connector -> normalizer -> historian
    /// -> buffer store-and-forward -> trust ladder.
    /// </summary>
    [TestClass]
    public class QueenTestE2E
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-queen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [TestMethod]
        public async Task Queen_scenario_detects_reads_persists_and_buffers()
        {
            // 1) "Prensa 1987" simulada (TCP enviando TEMP)
            var port = GetFreePort();
            var sim = new TcpSimulator(port);
            sim.Start();
            try
            {
                await Task.Delay(200);

                // 2) Discovery pasivo: detectar la prensa
                var discovery = new TcpPortDiscovery();
                var found = await discovery.ScanTcpAsync(IPAddress.Loopback, new[] { port }, TimeSpan.FromSeconds(1));
                Assert.AreEqual(1, found.Count);
                var endpoint = found[0];
                Assert.AreEqual(TrustTier.Passive, endpoint.TrustTier);

                // 3) Pipeline observador
                var historian = new FileObservationHistorian(Path.Combine(_tempDir, "historian"));
                var buffer = new FileStoreAndForwardBuffer(Path.Combine(_tempDir, "sf"));
                var ladder = new TrustLadder();
                var normalizer = new ObservationNormalizer();
                var factory = new TcpConnectorFactory();
                var connector = factory.Create(endpoint.Address);

                var obsSvc = new ObservationService(connector, normalizer, historian, ladder, endpoint);
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await obsSvc.StartAsync(cts.Token);
                await Task.Delay(2200);
                await obsSvc.StopAsync();

                // 4) Verificar: tier promovido a Observed
                Assert.AreEqual(TrustTier.Observed, endpoint.TrustTier);

                // 5) Verificar: observations persistidas
                var day = DateTime.UtcNow.ToString("yyyyMMdd");
                var persisted = 0;
                await foreach (var _ in historian.ReadDayAsync(day)) persisted++;
                Assert.IsTrue(persisted >= 1, "Historian debio guardar al menos 1 obs");

                // 6) Buffer store-and-forward: simular store + drain
                await buffer.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "TEMP", Value = 25 });
                var delivered = await buffer.DrainAsync((o, ct) => Task.FromResult(true), max: 10);
                Assert.AreEqual(1, delivered);
            }
            finally
            {
                sim.Stop();
                sim.Dispose();
            }
        }

        private static int GetFreePort()
        {
            using var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }
    }
}
