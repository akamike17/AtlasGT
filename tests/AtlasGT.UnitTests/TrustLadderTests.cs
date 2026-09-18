using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Models;
using AtlasGT.Security;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class TrustLadderTests
    {
        [TestMethod]
        public void Endpoint_starts_passive_by_default()
        {
            var ep = new Endpoint();
            Assert.AreEqual(TrustTier.Passive, ep.TrustTier);
        }

        [TestMethod]
        public void MarkObserved_promotes_passive_to_observed()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint();
            var result = ladder.MarkObserved(ep);
            Assert.AreEqual(TrustTier.Observed, result);
            Assert.AreEqual(TrustTier.Observed, ep.TrustTier);
            Assert.IsNotNull(ep.LastObservedAtUtc);
        }

        [TestMethod]
        public void MarkObserved_does_not_downgrade_higher_tiers()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint { TrustTier = TrustTier.ReadOnly };
            ladder.MarkObserved(ep);
            Assert.AreEqual(TrustTier.ReadOnly, ep.TrustTier);
        }

        [TestMethod]
        public void TryAuthorize_rejects_write_without_reason()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint();
            var ok = ladder.TryAuthorize(ep, TrustTier.WriteCapable, reason: "", authorizedBy: "tester");
            Assert.IsFalse(ok);
            Assert.AreEqual(TrustTier.Passive, ep.TrustTier);
        }

        [TestMethod]
        public void TryAuthorize_rejects_when_no_authorizer()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint();
            var ok = ladder.TryAuthorize(ep, TrustTier.ReadOnly, reason: "r", authorizedBy: "");
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryAuthorize_promotes_with_reason_and_authorizer()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint();
            var ok = ladder.TryAuthorize(ep, TrustTier.ReadOnly, "read-only authorized for commissioning", "admin");
            Assert.IsTrue(ok);
            Assert.AreEqual(TrustTier.ReadOnly, ep.TrustTier);
        }

        [TestMethod]
        public void TryAuthorize_idempotent_for_lower_target()
        {
            var ladder = new TrustLadder();
            var ep = new Endpoint { TrustTier = TrustTier.Verified };
            var ok = ladder.TryAuthorize(ep, TrustTier.Observed, "", "");
            Assert.IsTrue(ok);
            Assert.AreEqual(TrustTier.Verified, ep.TrustTier);
        }
    }
}
