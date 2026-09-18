using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Connectors.Mqtt;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;

namespace AtlasGT.EndToEndTests
{
    [TestClass]
    public class StoreAndForwardE2ETests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-sf-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [TestMethod]
        public async Task Observations_buffered_then_forwarded_to_mqtt()
        {
            // Setup
            var buffer = new FileStoreAndForwardBuffer(Path.Combine(_tempDir, "sf"));
            var broker = new InMemoryMqttBroker();
            var received = 0;
            await broker.SubscribeAsync("atlasgt/observations", (t, p) =>
            {
                Interlocked.Increment(ref received);
                return Task.CompletedTask;
            });

            var forwarder = new MqttForwarder(buffer, broker);

            // Meter 3 obs
            for (int i = 0; i < 3; i++)
                await buffer.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "TEMP", Value = 20 + i });

            Assert.AreEqual(3, await buffer.PendingCountAsync());
            Assert.AreEqual(0, received);

            // Flush
            var delivered = await forwarder.FlushAsync();
            Assert.AreEqual(3, delivered);
            Assert.AreEqual(0, await buffer.PendingCountAsync());
            Assert.AreEqual(3, received);
        }

        [TestMethod]
        public async Task Forwarder_is_idempotent_when_called_twice()
        {
            var buffer = new FileStoreAndForwardBuffer(Path.Combine(_tempDir, "sf"));
            var broker = new InMemoryMqttBroker();
            var received = 0;
            await broker.SubscribeAsync("atlasgt/observations", (t, p) =>
            {
                Interlocked.Increment(ref received);
                return Task.CompletedTask;
            });

            var forwarder = new MqttForwarder(buffer, broker);
            await buffer.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "TEMP", Value = 1 });

            await forwarder.FlushAsync();
            await forwarder.FlushAsync(); // segunda vez

            Assert.AreEqual(1, received);
        }
    }
}
