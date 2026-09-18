using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Domain.Models;
using AtlasGT.Historian;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class HistorianTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (IOException) { /* archivo aun bloqueado por SO */ }
            catch (UnauthorizedAccessException) { /* permisos */ }
        }

        [TestMethod]
        public async Task Appends_and_reads_back_observation()
        {
            var hist = new FileObservationHistorian(_tempDir);
            var obs = new Observation
            {
                Id = Guid.NewGuid(),
                Name = "TEMP",
                Value = 25.5,
                Unit = "°C",
                Transport = "tcp",
                TrustTier = TrustTier.Observed,
                ReceivedAtUtc = DateTimeOffset.UtcNow
            };
            await hist.AppendAsync(obs);

            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            var results = new List<Observation>();
            await foreach (var o in hist.ReadDayAsync(day))
                results.Add(o);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(obs.Id, results[0].Id);
            Assert.AreEqual(25.5, results[0].Value);
            Assert.AreEqual("TEMP", results[0].Name);
        }

        [TestMethod]
        public async Task Multiple_observations_persist_in_order()
        {
            var hist = new FileObservationHistorian(_tempDir);
            for (int i = 0; i < 5; i++)
            {
                await hist.AppendAsync(new Observation
                {
                    Id = Guid.NewGuid(),
                    Name = $"X{i}",
                    Value = i
                });
            }

            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            var list = new List<Observation>();
            await foreach (var o in hist.ReadDayAsync(day))
                list.Add(o);

            Assert.AreEqual(5, list.Count);
            CollectionAssert.AreEqual(
                new[] { 0.0, 1.0, 2.0, 3.0, 4.0 },
                list.Select(x => x.Value).ToArray());
        }

        [TestMethod]
        public async Task ReadDay_on_missing_file_returns_empty()
        {
            var hist = new FileObservationHistorian(_tempDir);
            var list = new List<Observation>();
            await foreach (var o in hist.ReadDayAsync("19990101"))
                list.Add(o);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public async Task Concurrent_appends_do_not_lose_data()
        {
            var hist = new FileObservationHistorian(_tempDir);
            const int writers = 8;
            const int perWriter = 10;

            var tasks = Enumerable.Range(0, writers).Select(async w =>
            {
                for (int i = 0; i < perWriter; i++)
                {
                    await hist.AppendAsync(new Observation
                    {
                        Id = Guid.NewGuid(),
                        Name = $"w{w}i{i}",
                        Value = i
                    });
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            var list = new List<Observation>();
            await foreach (var o in hist.ReadDayAsync(day))
                list.Add(o);

            Assert.AreEqual(writers * perWriter, list.Count);
        }
    }
}
