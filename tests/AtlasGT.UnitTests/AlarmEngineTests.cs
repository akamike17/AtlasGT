using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Application.Alarms;
using AtlasGT.Domain.Models;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class AlarmEngineTests
    {
        private static Observation Obs(double v, string name = "TEMP") => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = v
        };

        [TestMethod]
        public void Debounce_prevents_single_spike_from_raising()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "HighTemp", SignalKey = "TEMP", Threshold = 30, Debounce = 3 });

            var r1 = engine.Process(Obs(35));
            var r2 = engine.Process(Obs(36));
            Assert.AreEqual(0, r1.Count);
            Assert.AreEqual(0, r2.Count);
            Assert.AreEqual(0, engine.ActiveAlarms.Count);
        }

        [TestMethod]
        public void Fires_after_debounce_count_reached()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "HighTemp", SignalKey = "TEMP", Threshold = 30, Debounce = 3 });

            engine.Process(Obs(35));
            engine.Process(Obs(36));
            var raised = engine.Process(Obs(37));

            Assert.AreEqual(1, raised.Count);
            Assert.AreEqual(1, engine.ActiveAlarms.Count);
            Assert.AreEqual(AlarmState.Active, raised[0].State);
            Assert.AreEqual("HighTemp", raised[0].RuleName);
        }

        [TestMethod]
        public void Acknowledge_moves_to_acknowledged()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "X", SignalKey = "T", Threshold = 1, Debounce = 1 });
            var raised = engine.Process(Obs(5, "T"));
            Assert.AreEqual(1, raised.Count);

            var ok = engine.Acknowledge(raised[0].Id, "op1");
            Assert.IsTrue(ok);
            Assert.AreEqual(AlarmState.Acknowledged, engine.ActiveAlarms[0].State);
            Assert.AreEqual("op1", engine.ActiveAlarms[0].AcknowledgedBy);
        }

        [TestMethod]
        public void Clear_marks_cleared_and_removes_from_active()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "X", SignalKey = "T", Threshold = 1, Debounce = 1 });
            var raised = engine.Process(Obs(5, "T"));
            engine.Clear(raised[0].Id, "normalizo");
            Assert.AreEqual(0, engine.ActiveAlarms.Count);
        }

        [TestMethod]
        public void Rate_limit_blocks_storm()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "X", SignalKey = "T", Threshold = 0, Debounce = 1, MaxPerMinute = 2 });

            var r1 = engine.Process(Obs(1, "T"));
            var r2 = engine.Process(Obs(1, "T"));
            var r3 = engine.Process(Obs(1, "T"));

            Assert.AreEqual(1, r1.Count);
            Assert.AreEqual(1, r2.Count);
            Assert.AreEqual(0, r3.Count); // tormenta bloqueada
        }

        [TestMethod]
        public void Reset_debounce_allows_rerefire()
        {
            var engine = new AlarmEngine();
            engine.AddRule(new AlarmRule { Name = "X", SignalKey = "T", Threshold = 1, Debounce = 2 });

            engine.Process(Obs(5, "T"));
            engine.ResetDebounce("T");
            var r = engine.Process(Obs(5, "T")); // ahora no dispara, debounce reseteado
            Assert.AreEqual(0, r.Count);
        }
    }
}
