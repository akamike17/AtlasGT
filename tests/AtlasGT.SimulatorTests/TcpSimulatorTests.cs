using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;
using AtlasGT.Connectors.Simulators;

namespace AtlasGT.SimulatorTests
{
    [TestClass]
    public class TcpSimulatorTests
    {
        private static int GetFreePort()
        {
            using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [TestMethod]
        public async Task Simulator_sends_ascii_lines_over_tcp()
        {
            var port = GetFreePort();
            var sim = new TcpSimulator(port);
            sim.Start();
            try
            {
                await Task.Delay(150); // dejar que arranque

                var connector = new TcpConnector("127.0.0.1", port);
                var connected = await connector.ConnectAsync();
                Assert.IsTrue(connected);

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var samples = 0;
                string? received = null;

                await foreach (var sample in connector.ReadAllAsync(cts.Token))
                {
                    received = Encoding.ASCII.GetString(sample.Payload.Span);
                    samples++;
                    if (samples >= 1) break;
                }

                Assert.IsTrue(samples >= 1, "Se esperaba al menos 1 sample del simulador");
                Assert.IsNotNull(received);
                StringAssert.StartsWith(received, "TEMP:");

                await connector.DisposeAsync();
            }
            finally
            {
                sim.Stop();
                sim.Dispose();
            }
        }
    }
}
