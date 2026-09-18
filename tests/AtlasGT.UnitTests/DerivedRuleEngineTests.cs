using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Application.Rules;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class DerivedRuleEngineTests
    {
        [TestMethod]
        public void ThresholdAndPresent_rule_evaluates_correctly()
        {
            var rule = DerivedRuleEngine.ThresholdAndPresent("MACHINE_RUNNING", "current", 3.2, "cycle_pulse");
            var engine = new DerivedRuleEngine();
            engine.Add(rule);

            var signals = new Dictionary<string, double?>
            {
                ["current"] = 5.0,
                ["cycle_pulse"] = 1.0
            };
            var results = engine.EvaluateAll(signals);
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Value);

            // current baja
            signals["current"] = 1.0;
            results = engine.EvaluateAll(signals);
            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].Value);
        }

        [TestMethod]
        public void Rule_returns_null_when_signal_missing()
        {
            var rule = DerivedRuleEngine.ThresholdAndPresent("RUNNING", "current", 3.2, "cycle_pulse");
            var engine = new DerivedRuleEngine();
            engine.Add(rule);

            var results = engine.EvaluateAll(new Dictionary<string, double?> { ["current"] = 5.0 });
            Assert.AreEqual(0, results.Count); // no evaluable por falta de cycle_pulse
        }

        [TestMethod]
        public void Adding_rule_requires_evaluate()
        {
            var engine = new DerivedRuleEngine();
            var bad = new DerivedRule { OutputSignalKey = "X" };
            Assert.ThrowsException<ArgumentException>(() => engine.Add(bad));
        }
    }
}
