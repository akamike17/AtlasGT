using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;

namespace AtlasGT.ProtocolTests
{
    [TestClass]
    public class TcpConnectorProtocolTests
    {
        [TestMethod]
        public void Factory_creates_connector_for_tcp_scheme()
        {
            var factory = new TcpConnectorFactory();
            Assert.IsTrue(factory.CanHandle("tcp://127.0.0.1:5000"));
            Assert.IsFalse(factory.CanHandle("udp://127.0.0.1:5000"));
            Assert.IsFalse(factory.CanHandle("127.0.0.1:5000"));
        }

        [TestMethod]
        public void Factory_rejects_invalid_addresses()
        {
            var factory = new TcpConnectorFactory();
            Assert.ThrowsException<ArgumentException>(() => factory.Create("tcp://"));
            Assert.ThrowsException<ArgumentException>(() => factory.Create("tcp://host"));
            Assert.ThrowsException<ArgumentException>(() => factory.Create("tcp://host:notaport"));
            Assert.ThrowsException<ArgumentException>(() => factory.Create("tcp://host:0"));
            Assert.ThrowsException<ArgumentException>(() => factory.Create("tcp://host:99999"));
        }

        [TestMethod]
        public async Task Factory_returns_working_connector_address()
        {
            var factory = new TcpConnectorFactory();
            await using var connTask = factory.Create("tcp://127.0.0.1:5000");
            Assert.AreEqual("tcp://127.0.0.1:5000", connTask.EndpointAddress);
            Assert.AreEqual("tcp", connTask.TransportKind);
        }

        [TestMethod]
        public async Task Connector_fails_cleanly_when_nothing_listens()
        {
            var connector = new TcpConnector("127.0.0.1", 59997);
            var ok = await connector.ConnectAsync();
            Assert.IsFalse(ok);
            await connector.DisposeAsync();
        }
    }
}
