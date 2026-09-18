using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Models;
using AtlasGT.Normalization;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class ObservationNormalizerTests
    {
        private static RawSample MakeSample(string text)
        {
            return new RawSample
            {
                EndpointAddress = "tcp://127.0.0.1:5000",
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                Payload = Encoding.ASCII.GetBytes(text),
                TransportKind = "tcp"
            };
        }

        [TestMethod]
        public void Parses_KEY_value_ascii()
        {
            var n = new ObservationNormalizer();
            var obs = n.Normalize(MakeSample("TEMP:25.5"));
            Assert.AreEqual("TEMP", obs.Name);
            Assert.AreEqual(25.5, obs.Value);
            Assert.AreEqual("°C", obs.Unit);
            Assert.AreEqual(TrustTier.Observed, obs.TrustTier);
        }

        [TestMethod]
        public void Parses_plain_double()
        {
            var n = new ObservationNormalizer();
            var obs = n.Normalize(MakeSample("12.34"));
            Assert.AreEqual("value", obs.Name);
            Assert.AreEqual(12.34, obs.Value);
        }

        [TestMethod]
        public void Unparsable_text_becomes_raw_observation()
        {
            var n = new ObservationNormalizer();
            var obs = n.Normalize(MakeSample("HELLO WORLD"));
            Assert.AreEqual("text", obs.Name);
            Assert.IsNull(obs.Value);
            Assert.AreEqual("HELLO WORLD", obs.Description);
        }

        [TestMethod]
        public void Carries_raw_payload_and_transport()
        {
            var n = new ObservationNormalizer();
            var obs = n.Normalize(MakeSample("TEMP:1.0"));
            Assert.AreEqual("tcp", obs.Transport);
            Assert.IsNotNull(obs.RawPayload);
            Assert.AreEqual("TEMP:1.0", Encoding.ASCII.GetString(obs.RawPayload!).Trim());
        }

        [TestMethod]
        public void Endpoint_id_is_set()
        {
            var n = new ObservationNormalizer();
            var id = Guid.NewGuid();
            var obs = n.Normalize(MakeSample("TEMP:1.0"), endpointId: id);
            Assert.AreEqual(id, obs.EndpointId);
        }
    }
}
