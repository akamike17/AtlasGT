using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtlasGT.Infrastructure;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class AuditAndBackupTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch (IOException) { /* */ }
            catch (UnauthorizedAccessException) { /* */ }
        }

        [TestMethod]
        public async Task Audit_writes_appendable_lines()
        {
            var log = new FileAuditLog(_tempDir);
            await log.RecordAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                AtUtc = DateTimeOffset.UtcNow,
                Actor = "system",
                Action = "endpoint.authorize",
                TargetId = Guid.NewGuid().ToString(),
                Succeeded = true
            });
            await log.RecordAsync(new AuditEntry
            {
                Id = Guid.NewGuid(),
                AtUtc = DateTimeOffset.UtcNow,
                Actor = "user:admin",
                Action = "profile.import",
                TargetId = "ModbusDevice_v1",
                Succeeded = true
            });

            var path = Path.Combine(_tempDir, "audit.log");
            Assert.IsTrue(File.Exists(path));
            var lines = File.ReadAllLines(path);
            Assert.AreEqual(2, lines.Length);
            Assert.IsTrue(lines[0].Contains("endpoint.authorize"));
            Assert.IsTrue(lines[1].Contains("user:admin"));
        }

        [TestMethod]
        public async Task Backup_creates_zip_and_restore_brings_files_back()
        {
            // Crear algunos datos
            var dataDir = Path.Combine(_tempDir, "data");
            Directory.CreateDirectory(dataDir);
            await File.WriteAllTextAsync(Path.Combine(dataDir, "sample.txt"), "hello" , System.Text.Encoding.UTF8);

            var backup = new FileBackupService(dataDir);
            var zipPath = await backup.CreateBackupAsync();
            Assert.IsTrue(File.Exists(zipPath), $"Backup debio existir en {zipPath}");

            // Borrar el dato original
            File.Delete(Path.Combine(dataDir, "sample.txt"));
            Assert.IsFalse(File.Exists(Path.Combine(dataDir, "sample.txt")));

            // Restore
            var restored = await backup.RestoreAsync(zipPath);
            Assert.IsTrue(restored);
            Assert.IsTrue(File.Exists(Path.Combine(dataDir, "sample.txt")));
            var content = await File.ReadAllTextAsync(Path.Combine(dataDir, "sample.txt"));
            Assert.AreEqual("hello", content);
        }

        [TestMethod]
        public async Task Backup_list_empty_when_no_backups()
        {
            var backup = new FileBackupService(_tempDir);
            var list = await backup.ListBackupsAsync();
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public async Task Restore_returns_false_for_missing_zip()
        {
            var backup = new FileBackupService(_tempDir);
            var result = await backup.RestoreAsync(Path.Combine(_tempDir, "nonexistent.zip"));
            Assert.IsFalse(result);
        }
    }
}
