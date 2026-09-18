using System;
using System.Net;
using System.Net.Sockets;
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
    public class CountOrchestratorTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atlasgt-orch-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true); }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [TestMethod]
        public async Task Orchestrator_runs_multiple_machines_and_survives_partial_failures()
        {
            // 1 simulador bueno + 1 endpoint apuntando a puerto cerrado
            var goodPort = FreePort();
            var sim = new TcpSimulator(goodPort);
            sim.Start();
            await Task.Delay(150);

            var historian = new FileObservationHistorian(_tempDir);
            var normalizer = new ObservationNormalizer();
            var ladder = new TrustLadder();
            var factory = new TcpConnectorFactory();

            var goodEp = new Endpoint { Id = Guid.NewGuid(), Address = $"tcp://127.0.0.1:{goodPort}" };
            var badEp = new Endpoint { Id = Guid.NewGuid(), Address = $"tcp://127.0.0.1:{FreePort()}" }; // sin sim

            var endpoints = new[]
            {
                (goodEp, factory.Create(goodEp.Address)),
                (badEp, factory.Create(badEp.Address))
            };

            await using var orch = new CountOrchestrator();
            var started = await orch.AttachEndpointsAsync(endpoints, normalizer, historian, ladder);

            Assert.AreEqual(1, started, "Solo 1 endpoint debia arrancar (el bueno)");

            await Task.Delay(2200);
            await orch.StopAllAsync();
            sim.Stop(); sim.Dispose();

            Assert.IsTrue(orch.TotalObservations >= 1, "Debio observar al menos 1 dato");
            Assert.AreEqual(TrustTier.Observed, goodEp.TrustTier);
        }

        private static int FreePort()
        {
            using var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }
    }
}
