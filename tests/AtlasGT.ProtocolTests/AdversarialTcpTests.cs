using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;

namespace AtlasGT.ProtocolTests
{
    /// <summary>Pruebas adversas (sec. 28) para el conector TCP.</summary>
    [TestClass]
    public class AdversarialTcpTests
    {
        private static int FreePort()
        {
            using var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        [TestMethod]
        public async Task Connector_handles_server_disconnecting_mid_stream()
        {
            var port = FreePort();
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            var acceptTask = listener.AcceptTcpClientAsync();

            await using var conn = new TcpConnector("127.0.0.1", port);
            Assert.IsTrue(await conn.ConnectAsync());

            var serverClient = await acceptTask;
            // Server manda una linea completa y cierra
            var bytes = Encoding.ASCII.GetBytes("TEMP:99.9\n");
            await serverClient.GetStream().WriteAsync(bytes, 0, bytes.Length);
            serverClient.Close();
            listener.Stop();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var got = 0;
            try
            {
                await foreach (var sample in conn.ReadAllAsync(cts.Token)) got++;
            }
            catch (OperationCanceledException) { /* ok - read loop cancels when token cancelled */ }

            Assert.IsTrue(got >= 1, $"Debio leer 1 linea, leyo {got}");
        }

        [TestMethod]
        public async Task Connector_handles_malformed_incomplete_data_without_dying()
        {
            var port = FreePort();
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            var acceptTask = listener.AcceptTcpClientAsync();

            await using var conn = new TcpConnector("127.0.0.1", port);
            await conn.ConnectAsync();

            var serverClient = await acceptTask;
            // Datos sin newline + basura
            var bytes = Encoding.ASCII.GetBytes("GARBAGE_NO_NEWLINE\xFF\xFE\x00");
            await serverClient.GetStream().WriteAsync(bytes, 0, bytes.Length);

            var cts = new CancellationTokenSource(1500);
            var got = 0;
            try
            {
                await foreach (var sample in conn.ReadAllAsync(cts.Token)) got++;
            }
            catch (OperationCanceledException) { }

            Assert.AreEqual(0, got, "Sin newline, no debe emitir");

            serverClient.Close();
            listener.Stop();
        }

        [TestMethod]
        public async Task Connector_returns_false_when_server_absent()
        {
            var port = FreePort();
            await using var conn = new TcpConnector("127.0.0.1", port);
            Assert.IsFalse(await conn.ConnectAsync());
            Assert.AreEqual(AtlasGT.Connectors.Abstractions.ConnectorState.Faulted, conn.State);
        }
    }
}
