using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Network;

namespace AtlasGT.ProtocolTests
{
    [TestClass]
    public class HttpConnectorTests
    {
        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly string _body;
            public FakeHandler(string body) { _body = body; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, Encoding.ASCII, "text/plain")
                };
                return Task.FromResult(resp);
            }
        }

        [TestMethod]
        public async Task HttpConnector_polls_and_returns_payload()
        {
            var handler = new FakeHandler("TEMP:21.5");
            var uri = new Uri("http://fake.local/metrics");
            await using var connector = new HttpConnector(uri, TimeSpan.FromMilliseconds(100), handler);

            var ok = await connector.ConnectAsync();
            Assert.IsTrue(ok);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var sampleCount = 0;
            await foreach (var sample in connector.ReadAllAsync(cts.Token))
            {
                sampleCount++;
                var text = Encoding.ASCII.GetString(sample.Payload.Span);
                Assert.AreEqual("TEMP:21.5", text);
                if (sampleCount >= 2) break;
            }

            Assert.IsTrue(sampleCount >= 2, "Debio recibir al menos 2 muestras del polling");
        }

        [TestMethod]
        public void HttpFactory_accepts_http_and_https()
        {
            var f = new HttpConnectorFactory();
            Assert.IsTrue(f.CanHandle("http://a.b/c"));
            Assert.IsTrue(f.CanHandle("https://a.b/c"));
            Assert.IsFalse(f.CanHandle("tcp://x"));
        }
    }
}
