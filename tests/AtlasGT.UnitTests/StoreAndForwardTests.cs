using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class StoreAndForwardTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-sf-" + Guid.NewGuid().ToString("N"));
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
        public async Task Enqueue_persists_pending()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            await buf.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "T", Value = 1.0 });
            Assert.AreEqual(1, await buf.PendingCountAsync());
        }

        [TestMethod]
        public async Task Enqueue_is_idempotent_by_id()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            var id = Guid.NewGuid();
            await buf.EnqueueAsync(new Observation { Id = id, Name = "T", Value = 1.0 });
            await buf.EnqueueAsync(new Observation { Id = id, Name = "T", Value = 1.0 });
            Assert.AreEqual(1, await buf.PendingCountAsync());
        }

        [TestMethod]
        public async Task Drain_moves_successful_to_sent()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            for (int i = 0; i < 3; i++)
                await buf.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "T", Value = i });

            int delivered = await buf.DrainAsync((obs, ct) => Task.FromResult(true), max: 10);
            Assert.AreEqual(3, delivered);
            Assert.AreEqual(0, await buf.PendingCountAsync());
        }

        [TestMethod]
        public async Task Drain_keeps_failed_items_pending_for_retry()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            await buf.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "T", Value = 1 });

            int delivered = await buf.DrainAsync((obs, ct) => Task.FromResult(false), max: 10);
            Assert.AreEqual(0, delivered);
            Assert.AreEqual(1, await buf.PendingCountAsync());

            // Reintento exitoso
            delivered = await buf.DrainAsync((obs, ct) => Task.FromResult(true), max: 10);
            Assert.AreEqual(1, delivered);
            Assert.AreEqual(0, await buf.PendingCountAsync());
        }

        [TestMethod]
        public async Task Drain_respects_max_limit()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            for (int i = 0; i < 5; i++)
                await buf.EnqueueAsync(new Observation { Id = Guid.NewGuid(), Name = "T", Value = i });

            int delivered = await buf.DrainAsync((obs, ct) => Task.FromResult(true), max: 2);
            Assert.AreEqual(2, delivered);
            Assert.AreEqual(3, await buf.PendingCountAsync());
        }

        [TestMethod]
        public async Task Destination_throwing_does_not_lose_item()
        {
            var buf = new FileStoreAndForwardBuffer(_tempDir);
            var id = Guid.NewGuid();
            await buf.EnqueueAsync(new Observation { Id = id, Name = "T", Value = 1 });

            int delivered = await buf.DrainAsync((obs, ct) => throw new InvalidOperationException("destino muerto"), max: 1);
            Assert.AreEqual(0, delivered);
            Assert.AreEqual(1, await buf.PendingCountAsync());
        }
    }
}
