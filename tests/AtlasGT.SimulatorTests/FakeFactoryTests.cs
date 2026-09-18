using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;
using AtlasGT.Connectors.Simulators;

namespace AtlasGT.SimulatorTests
{
    [TestClass]
    public class FakeFactoryTests
    {
        [TestMethod]
        public async Task FakeFactory_starts_N_machines()
        {
            var opts = new FakeFactoryOptions { MachineCount = 3, BasePort = 6100 };
            using var ff = new FakeFactory(opts);
            ff.Start();

            Assert.AreEqual(3, ff.Machines.Count);
            ff.StopAll();
        }

        [TestMethod]
        public void FakeFactory_malformed_mode_returns_bad_data()
        {
            var opts = new FakeFactoryOptions
            {
                MachineCount = 1,
                EnableMalformedData = true,
                MalformedDataProbability = 1.0 // siempre malformado
            };
            var ff = new FakeFactory(opts);

            var msg = ff.GetNextMessage(0, 0);
            Assert.IsTrue(
                msg == "GARBAGE" || msg == "TEMP:not_a_number" || msg == string.Empty,
                $"Se esperaba malformado, obtuvo: '{msg}'");
        }

        [TestMethod]
        public void FakeFactory_normal_mode_returns_valid_TEMP()
        {
            var opts = new FakeFactoryOptions { MachineCount = 1, EnableMalformedData = false };
            var ff = new FakeFactory(opts);

            var msg = ff.GetNextMessage(0, 0);
            Assert.IsTrue(msg.StartsWith("TEMP:"), $"Esperaba TEMP:*, obtuvo '{msg}'");

            var value = double.Parse(msg.Substring(5));
            Assert.IsTrue(value >= 20.0 && value <= 30.0, $"Valor fuera de rango: {value}");
        }
    }
}
