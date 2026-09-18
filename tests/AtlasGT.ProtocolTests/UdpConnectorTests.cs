using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;

namespace AtlasGT.ProtocolTests
{
    [TestClass]
    public class UdpConnectorTests
    {
        [TestMethod]
        public async Task UdpConnector_receives_datagram()
        {
            var port = GetFreePort();
            await using var connector = new UdpConnector(port);
            var started = await connector.ConnectAsync();
            Assert.IsTrue(started);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var readTask = Task.Run(async () =>
            {
                await foreach (var sample in connector.ReadAllAsync(cts.Token))
                    return sample;
                return null;
            });

            await Task.Delay(200);

            using (var sender = new UdpClient())
            {
                var payload = Encoding.ASCII.GetBytes("TEMP:22.5");
                await sender.SendAsync(payload, payload.Length, "127.0.0.1", port);
            }

            var sample = await readTask;
            Assert.IsNotNull(sample);
            var text = Encoding.ASCII.GetString(sample.Payload.Span);
            Assert.AreEqual("TEMP:22.5", text);
            Assert.AreEqual("udp", sample.TransportKind);
        }

        [TestMethod]
        public void UdpFactory_creates_connector()
        {
            var f = new UdpConnectorFactory();
            Assert.IsTrue(f.CanHandle("udp://:5000"));
            Assert.IsTrue(f.CanHandle("udp://127.0.0.1:5000"));
            Assert.IsFalse(f.CanHandle("tcp://127.0.0.1:5000"));
        }

        private static int GetFreePort()
        {
            using var udp = new UdpClient(0);
            var port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            udp.Close();
            return port;
        }
    }
}
