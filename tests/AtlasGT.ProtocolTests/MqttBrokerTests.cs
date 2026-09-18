using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Mqtt;

namespace AtlasGT.ProtocolTests
{
    [TestClass]
    public class MqttBrokerTests
    {
        [TestMethod]
        public async Task Publish_invokes_subscriber_handler()
        {
            var broker = new InMemoryMqttBroker();
            var received = 0;
            string? last = null;

            await broker.SubscribeAsync("topic/a", async (t, p) =>
            {
                received++;
                last = Encoding.ASCII.GetString(p);
                await Task.CompletedTask;
            });

            await broker.PublishAsync("topic/a", Encoding.ASCII.GetBytes("hola"));
            Assert.AreEqual(1, received);
            Assert.AreEqual("hola", last);
        }

        [TestMethod]
        public async Task Unsubscribe_stops_delivery()
        {
            var broker = new InMemoryMqttBroker();
            var count = 0;
            Func<string, byte[], Task> h = (t, p) => { count++; return Task.CompletedTask; };

            await broker.SubscribeAsync("topic/x", h);
            await broker.PublishAsync("topic/x", new byte[1]);
            Assert.AreEqual(1, count);

            await broker.UnsubscribeAsync("topic/x", h);
            await broker.PublishAsync("topic/x", new byte[1]);
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public async Task Subscriber_count_tracks_lifecycle()
        {
            var broker = new InMemoryMqttBroker();
            Assert.AreEqual(0, await broker.GetSubscriberCountAsync("a"));

            Func<string, byte[], Task> h = (t, p) => Task.CompletedTask;
            await broker.SubscribeAsync("a", h);
            Assert.AreEqual(1, await broker.GetSubscriberCountAsync("a"));

            await broker.UnsubscribeAsync("a", h);
            Assert.AreEqual(0, await broker.GetSubscriberCountAsync("a"));
        }
    }
}
