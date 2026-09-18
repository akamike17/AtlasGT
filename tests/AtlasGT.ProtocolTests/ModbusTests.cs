using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Modbus;
using AtlasGT.Connectors.Simulators;

namespace AtlasGT.ProtocolTests
{
    [TestClass]
    public class ModbusTests
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
        public async Task Modbus_read_holding_registers_returns_values()
        {
            var port = FreePort();
            var sim = new ModbusTcpSimulator(port, new ushort[] { 73, 1, 18452 % 65536, 42 });
            sim.Start();
            try
            {
                // Esperar a que el listener este listo
                var ready = false;
                for (int i = 0; i < 20 && !ready; i++)
                {
                    try
                    {
                        using var probe = new TcpClient();
                        probe.Connect("127.0.0.1", port);
                        ready = true;
                    }
                    catch (SocketException) { await Task.Delay(100); }
                }
                Assert.IsTrue(ready, "Simulador Modbus nunca respondio al puerto");

                var requests = new List<ModbusReadRequest>
                {
                    new() { FunctionCode = 3, StartAddress = 0, Quantity = 4 }
                };
                await using var connector = new ModbusTcpConnector("127.0.0.1", port, 1, requests, TimeSpan.FromMilliseconds(200));
                var ok = await connector.ConnectAsync();
                Assert.IsTrue(ok);

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await foreach (var sample in connector.ReadAllAsync(cts.Token))
                {
                    var regs = ModbusResponseDecoder.DecodeRegisters(sample.Payload.ToArray());
                    Assert.IsNotNull(regs);
                    Assert.AreEqual(4, regs!.Length);
                    Assert.AreEqual(73, regs[0]);
                    Assert.AreEqual(1, regs[1]);
                    Assert.AreEqual(18452 % 65536, regs[2]);
                    Assert.AreEqual(42, regs[3]);
                    return;
                }
                Assert.Fail("No se recibio respuesta Modbus");
            }
            finally { sim.Stop(); sim.Dispose(); }
        }

        [TestMethod]
        public void Decoder_rejects_garbage()
        {
            Assert.IsNull(ModbusResponseDecoder.DecodeRegisters(Array.Empty<byte>()));
            Assert.IsNull(ModbusResponseDecoder.DecodeRegisters(new byte[] { 1, 2, 3 }));
            Assert.IsNull(ModbusResponseDecoder.DecodeRegisters(new byte[] { 0, 0, 0, 0, 0, 6, 1, 99, 4, 0, 1 }));
        }
    }
}
