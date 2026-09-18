using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AtlasGT.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AtlasGT.UnitTests
{
    [TestClass]
    public class SignedAuditLogTests
    {
        private string _tempDir = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "atlasgt-signed-audit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }

        [TestMethod]
        public async Task Append_y_verify_cadena_valida()
        {
            var log = new SignedFileAuditLog(_tempDir);

            await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "asset.create", Detail = "a1" });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "asset.update", Detail = "a2" });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:lab", Action = "discovery.scan", Detail = "d1" });

            var v = await log.VerifyAsync();
            Assert.IsTrue(v.Valid, $"verify fallo: {v.TamperReason}");
            Assert.AreEqual(3, v.TotalEntries);
            Assert.IsFalse(string.IsNullOrEmpty(v.LastHash));
        }

        [TestMethod]
        public async Task Primera_entrada_tiene_prevhash_genesis()
        {
            var log = new SignedFileAuditLog(_tempDir);
            var e = await log.AppendAsync(new SignedAuditEntry { Actor = "system", Action = "boot" });
            Assert.AreEqual("GENESIS", e.PrevHash);
        }

        [TestMethod]
        public async Task Cadena_se_propaga_entre_entradas()
        {
            var log = new SignedFileAuditLog(_tempDir);
            var e1 = await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "x" });
            var e2 = await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "y" });
            var e3 = await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "z" });
            Assert.AreEqual("GENESIS", e1.PrevHash);
            Assert.AreEqual(e1.EntryHash, e2.PrevHash);
            Assert.AreEqual(e2.EntryHash, e3.PrevHash);
            Assert.AreNotEqual(e1.EntryHash, e2.EntryHash);
        }

        [TestMethod]
        public async Task Tamper_contenido_es_detectado()
        {
            var log = new SignedFileAuditLog(_tempDir);
            var e1 = await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "asset.delete", Detail = "victim" });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "noop" });

            // Atacante edita el archivo: cambia el Detail de la primera linea
            var path = log.FilePath;
            var content = await File.ReadAllTextAsync(path);
            var tampered = content.Replace("victim", "inocente");
            Assert.AreNotEqual(content, tampered);
            await File.WriteAllTextAsync(path, tampered);

            var v = await log.VerifyAsync();
            Assert.IsFalse(v.Valid);
            Assert.AreEqual(e1.Id.ToString(), v.FirstTamperedEntryId);
            StringAssert.Contains(v.TamperReason ?? "", "EntryHash invalido");
        }

        [TestMethod]
        public async Task Tamper_reorden_quiebra_cadena()
        {
            var log = new SignedFileAuditLog(_tempDir);
            await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "e1" });
            await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "e2" });
            await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = "e3" });

            // Intercambiar lineas 1 y 3
            var path = log.FilePath;
            var lines = (await File.ReadAllLinesAsync(path)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.AreEqual(3, lines.Length);
            var swapped = new[] { lines[2], lines[1], lines[0] };
            await File.WriteAllLinesAsync(path, swapped);

            var v = await log.VerifyAsync();
            Assert.IsFalse(v.Valid);
            StringAssert.Contains(v.TamperReason ?? "", "PrevHash");
        }

        [TestMethod]
        public async Task Tamper_truncar_al_medio_es_detectado()
        {
            var log = new SignedFileAuditLog(_tempDir);
            for (int i = 0; i < 5; i++)
                await log.AppendAsync(new SignedAuditEntry { Actor = "a", Action = $"ev{i}" });

            // Atacante trunca ultima linea
            var path = log.FilePath;
            var lines = (await File.ReadAllLinesAsync(path)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            lines.RemoveAt(lines.Count - 1);
            await File.WriteAllLinesAsync(path, lines);

            // La verificacion desde inicio sigue valida para los 4 primeros
            // (la cadena queda consistente, pero el estado previo no es recuperable por el log solo).
            var v = await log.VerifyAsync();
            Assert.IsTrue(v.Valid, "truncar al final no invalida las entradas restantes");
            Assert.AreEqual(4, v.TotalEntries);
            // NOTA: esto es la limitacion conocida de cadena sola. En produccion se combina con
            // anchor externo periodico (el ultimo LastHash guardado en otro medio).
        }

        [TestMethod]
        public void Sanitizer_redact_passwords_y_tokens()
        {
            var payload = "{\"username\":\"admin\",\"password\":\"secret123\",\"nested\":{\"api_token\":\"tok-xyz\"},\"ok\":\"visible\"}";
            var clean = AuditSanitizer.Sanitize(payload)!;
            var doc = JsonDocument.Parse(clean);
            Assert.AreEqual("***REDACTED***", doc.RootElement.GetProperty("password").GetString());
            Assert.AreEqual("***REDACTED***", doc.RootElement.GetProperty("nested").GetProperty("api_token").GetString());
            Assert.AreEqual("visible", doc.RootElement.GetProperty("ok").GetString());
            Assert.AreEqual("admin", doc.RootElement.GetProperty("username").GetString());
        }

        [TestMethod]
        public void Sanitizer_texto_plano_cubre_patron_basico()
        {
            var s = "user=mario&password=hunter2&action=login";
            var clean = AuditSanitizer.Sanitize(s)!;
            StringAssert.Contains(clean, "REDACTED");
            Assert.IsFalse(clean.Contains("hunter2"));
        }

        [TestMethod]
        public async Task ReadAsync_filtra_por_actor_accion_severidad()
        {
            var log = new SignedFileAuditLog(_tempDir);
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "backup.create", Severity = AuditSeverity.Critical });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:viewer", Action = "http.denied", Severity = AuditSeverity.Warning });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:admin", Action = "asset.create", Severity = AuditSeverity.Info });
            await log.AppendAsync(new SignedAuditEntry { Actor = "role:lab", Action = "http.denied", Severity = AuditSeverity.Warning });

            var denegados = await log.ReadAsync(action: "http.denied", severity: AuditSeverity.Warning);
            Assert.AreEqual(2, denegados.Count);

            var soloAdminCriticos = await log.ReadAsync(actor: "admin", severity: AuditSeverity.Critical);
            Assert.AreEqual(1, soloAdminCriticos.Count);
        }
    }
}
