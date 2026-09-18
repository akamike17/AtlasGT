using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Simulators;
using AtlasGT.Discovery;
using AtlasGT.Domain.Models;

namespace AtlasGT.IntegrationTests
{
    [TestClass]
    public class DiscoveryTests
    {
        [TestMethod]
        public async Task Discovery_finds_open_tcp_ports_passively()
        {
            // Arrange: levantar 2 simuladores en puertos libres
            using var l1 = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l1.Start();
            var port1 = ((IPEndPoint)l1.LocalEndpoint).Port;
            l1.Stop();

            using var l2 = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l2.Start();
            var port2 = ((IPEndPoint)l2.LocalEndpoint).Port;
            l2.Stop();

            var sim1 = new TcpSimulator(port1);
            var sim2 = new TcpSimulator(port2);
            sim1.Start();
            sim2.Start();

            try
            {
                await Task.Delay(200);

                var discovery = new TcpPortDiscovery();
                var ports = Enumerable.Range(Math.Min(port1, port2) - 2, 8).ToList();
                var found = await discovery.ScanTcpAsync(
                    IPAddress.Loopback,
                    ports,
                    perPortTimeout: TimeSpan.FromMilliseconds(500));

                Assert.IsTrue(found.Any(e => e.Address == $"tcp://127.0.0.1:{port1}"),
                    $"Debia encontrar puerto {port1}");
                Assert.IsTrue(found.Any(e => e.Address == $"tcp://127.0.0.1:{port2}"),
                    $"Debia encontrar puerto {port2}");

                foreach (var ep in found)
                    Assert.AreEqual(TrustTier.Passive, ep.TrustTier);
            }
            finally
            {
                sim1.Stop(); sim1.Dispose();
                sim2.Stop(); sim2.Dispose();
            }
        }

        [TestMethod]
        public async Task Discovery_returns_empty_when_no_ports_open()
        {
            var discovery = new TcpPortDiscovery();
            var results = await discovery.ScanTcpAsync(
                IPAddress.Loopback,
                new[] { 59999, 59998 },
                perPortTimeout: TimeSpan.FromMilliseconds(200));
            Assert.AreEqual(0, results.Count);
        }
    }
}
