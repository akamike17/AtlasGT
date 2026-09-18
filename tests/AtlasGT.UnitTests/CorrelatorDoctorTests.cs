using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Application;
using AtlasGT.Domain.Models;
using AtlasGT.ProtocolInference;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class CorrelatorTests
    {
        [TestMethod]
        public void Correlator_detects_changed_signals()
        {
            var c = new SignalCorrelator();
            var before = new List<SignalCorrelator.SignalSnapshot>
            {
                new() { Key = "R17", Value = 0 },
                new() { Key = "R23", Value = 481 }
            };
            var after = new List<SignalCorrelator.SignalSnapshot>
            {
                new() { Key = "R17", Value = 1 },
                new() { Key = "R23", Value = 481 }
            };

            var hypotheses = c.Correlate(before, after).ToList();
            Assert.AreEqual(1, hypotheses.Count);
            Assert.AreEqual("R17", hypotheses[0].SignalKey);
            Assert.AreEqual(ProvenanceKind.Inferred, hypotheses[0].Confidence);
        }

        [TestMethod]
        public void Correlator_returns_empty_when_nothing_changed()
        {
            var c = new SignalCorrelator();
            var data = new List<SignalCorrelator.SignalSnapshot> { new() { Key = "R17", Value = 0 } };
            var hypotheses = c.Correlate(data, data).ToList();
            Assert.AreEqual(0, hypotheses.Count);
        }

        [TestMethod]
        public void Correlator_handles_null_inputs()
        {
            var c = new SignalCorrelator();
            var r = c.Correlate(null!, null!).ToList();
            Assert.AreEqual(0, r.Count);
        }
    }

    [TestClass]
    public class DoctorServiceTests
    {
        [TestMethod]
        public void Diagnose_offline_endpoint()
        {
            var doctor = new DoctorService();
            var ep = new Endpoint { Address = "tcp://x:1" };
            var report = doctor.Diagnose(ep);
            Assert.AreEqual("OFFLINE", report.Verdict);
        }

        [TestMethod]
        public void Diagnose_online_endpoint()
        {
            var doctor = new DoctorService();
            var ep = new Endpoint
            {
                Address = "tcp://x:1",
                TrustTier = TrustTier.Observed,
                LastObservedAtUtc = DateTime.UtcNow
            };
            var report = doctor.Diagnose(ep);
            Assert.AreEqual("ONLINE", report.Verdict);
            Assert.IsNull(report.SuspectedIssue);
        }

        [TestMethod]
        public void Diagnose_stale_endpoint()
        {
            var doctor = new DoctorService();
            var ep = new Endpoint
            {
                Address = "tcp://x:1",
                TrustTier = TrustTier.Observed,
                LastObservedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            };
            var report = doctor.Diagnose(ep);
            Assert.AreEqual("STALE", report.Verdict);
        }

        [TestMethod]
        public void Diagnose_throws_on_null()
        {
            var doctor = new DoctorService();
            Assert.ThrowsException<ArgumentNullException>(() => doctor.Diagnose(null!));
        }
    }
}
