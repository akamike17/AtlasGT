using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AtlasGT.EndToEndTests
{
    /// <summary>
    /// Pruebas de humo de la API HTTP sobre WebApplicationFactory.
    /// Cada test corre con un ConfigStore aislado en temp dir.
    /// Tras RoleGateMiddleware, las escrituras requieren header X-Atlas-Role: admin.
    /// </summary>
    [TestClass]
    public class ApiSmokeTests
    {
        private static (WebApplicationFactory<Program> Factory, string DataRoot) CreateFactory()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "atlas_api_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("AtlasGT:ConfigPath", Path.Combine(tmp, "config", "config.json")),
                        new KeyValuePair<string, string?>("AtlasGT:DataRoot", tmp)
                    });
                });
            });
            return (factory, tmp);
        }

        private static HttpClient AdminClient(WebApplicationFactory<Program> f)
        {
            var c = f.CreateClient();
            c.DefaultRequestHeaders.Add("X-Atlas-Role", "admin");
            return c;
        }

        [TestMethod]
        public async Task Health_responde_ok()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = factory.CreateClient();
            var r = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
        }

        [TestMethod]
        public async Task Assets_crud_fluye()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);

            var created = await client.PostAsJsonAsync("/api/assets", new { name = "PRENSA-04", tag = "PRENSA-04" });
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
            var asset = await created.Content.ReadFromJsonAsync<AtlasGT.Domain.Models.Asset>();
            Assert.IsNotNull(asset);
            Assert.AreNotEqual(Guid.Empty, asset!.Id);

            var list = await client.GetFromJsonAsync<AtlasGT.Domain.Models.Asset[]>("/api/assets");
            Assert.IsTrue(list!.Length >= 1);

            var fetched = await client.GetFromJsonAsync<AtlasGT.Domain.Models.Asset>($"/api/assets/{asset.Id}");
            Assert.AreEqual(asset.Name, fetched!.Name);

            var del = await client.DeleteAsync($"/api/assets/{asset.Id}");
            Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);
        }

        [TestMethod]
        public async Task Profiles_export_import_roundtrip_idempotente()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);

            await client.PostAsJsonAsync("/api/profiles", new { manufacturer = "Acme", model = "X1", protocol = "HTTP" });
            var exportJson = await client.GetStringAsync("/api/profiles/export");
            var doc = JsonDocument.Parse(exportJson);
            Assert.IsTrue(doc.RootElement.GetProperty("profiles").GetArrayLength() >= 1);

            // Reimportar no duplica
            var profilesJson = doc.RootElement.GetProperty("profiles").GetRawText();
            var impResp = await client.PostAsync("/api/profiles/import",
                new StringContent(profilesJson, System.Text.Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, impResp.StatusCode, await impResp.Content.ReadAsStringAsync());
            var impBody = await impResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.AreEqual(0, impBody.GetProperty("imported").GetInt32());
        }

        [TestMethod]
        public async Task Alarm_rule_persiste_en_configstore()
        {
            var (factory, dataRoot) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);

            var post = await client.PostAsJsonAsync("/api/alarms/rules", new
            {
                name = "Temp alta",
                signalKey = "temp",
                threshold = 50.0,
                comparison = "GreaterThan",
                severity = "Warning"
            });
            Assert.AreEqual(HttpStatusCode.Created, post.StatusCode);

            var cfgPath = Path.Combine(dataRoot, "config", "config.json");
            Assert.IsTrue(File.Exists(cfgPath), "config.json debe existir tras crear regla");
            var json = await File.ReadAllTextAsync(cfgPath);
            StringAssert.Contains(json, "Temp alta");
        }

        [TestMethod]
        public async Task Config_persiste_entre_instancias_del_store()
        {
            // Dos factories separadas apuntando al mismo directorio: la segunda lee lo que la primera guardo.
            var tmp = Path.Combine(Path.GetTempPath(), "atlas_restart_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            var cfgPath = Path.Combine(tmp, "config", "config.json");

            Guid assetId;
            using (var f1 = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("AtlasGT:ConfigPath", cfgPath),
                    new KeyValuePair<string, string?>("AtlasGT:DataRoot", tmp)
                }))))
            {
                using var c1 = f1.CreateClient();
                c1.DefaultRequestHeaders.Add("X-Atlas-Role", "admin");
                var r = await c1.PostAsJsonAsync("/api/assets", new { name = "Persiste Tras Restart", tag = "PR-01" });
                r.EnsureSuccessStatusCode();
                var a = await r.Content.ReadFromJsonAsync<AtlasGT.Domain.Models.Asset>();
                assetId = a!.Id;
            }

            using (var f2 = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string?>("AtlasGT:ConfigPath", cfgPath),
                    new KeyValuePair<string, string?>("AtlasGT:DataRoot", tmp)
                }))))
            {
                using var c2 = f2.CreateClient(); // viewer: lectura libre
                var list = await c2.GetFromJsonAsync<AtlasGT.Domain.Models.Asset[]>("/api/assets");
                Assert.IsTrue(list!.Length >= 1, "el segundo host debe leer el asset creado por el primero");
                Assert.IsTrue(Array.Exists(list, a => a.Id == assetId));
            }
        }

        [TestMethod]
        public async Task Backup_y_restore_ciclo_completo()
        {
            var (factory, dataRoot) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);

            // Sembrar estado
            var create = await client.PostAsJsonAsync("/api/assets", new { name = "Para backup", tag = "BK-01" });
            create.EnsureSuccessStatusCode();
            var cfgPath = Path.Combine(dataRoot, "config", "config.json");
            
            // El save es asincrono; esperar hasta que exista o 5s
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!File.Exists(cfgPath) && sw.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(100);
            Assert.IsTrue(File.Exists(cfgPath), $"config.json nunca aparecio en {cfgPath}. Contenido de {dataRoot}: {string.Join(",", Directory.GetFileSystemEntries(dataRoot, "*", SearchOption.AllDirectories))}");

            // Backup
            var bk = await client.PostAsync("/api/admin/backup", null);
            Assert.AreEqual(HttpStatusCode.OK, bk.StatusCode, await bk.Content.ReadAsStringAsync());
            var bkBody = await bk.Content.ReadFromJsonAsync<JsonElement>();
            var backupPath = bkBody.GetProperty("path").GetString()!;
            Assert.IsTrue(File.Exists(backupPath), "el zip debe existir");

            // Verificar que el zip contiene el config.json
            using (var zip = System.IO.Compression.ZipFile.OpenRead(backupPath))
            {
                var names = zip.Entries.Select(e => e.FullName).ToList();
                Assert.IsTrue(names.Any(n => n.Replace('\\', '/').EndsWith("config/config.json", StringComparison.OrdinalIgnoreCase)),
                    "el zip no contiene config/config.json; contenido: " + string.Join(",", names));
            }

            // Borrar config y restaurar
            File.Delete(cfgPath);
            Assert.IsFalse(File.Exists(cfgPath));

            var restore = await client.PostAsJsonAsync("/api/admin/restore", new { zipPath = backupPath });
            Assert.AreEqual(HttpStatusCode.OK, restore.StatusCode, await restore.Content.ReadAsStringAsync());
            Assert.IsTrue(File.Exists(cfgPath),
                $"config.json debe volver tras restore. dataRoot={dataRoot} cfgPath={cfgPath}");

            // Verificar contenido restaurado
            var list = await client.GetFromJsonAsync<AtlasGT.Domain.Models.Asset[]>("/api/assets");
            Assert.IsTrue(Array.Exists(list!, a => a.Name == "Para backup"));
        }

        [TestMethod]
        public async Task Write_sin_role_admin_es_rechazado()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = factory.CreateClient(); // viewer

            var r = await client.PostAsJsonAsync("/api/assets", new { name = "Bloqueado" });
            Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
        }

        [TestMethod]
        public async Task Discovery_requiere_rol_admin_o_lab()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;

            using var viewer = factory.CreateClient(); // sin rol
            var r1 = await viewer.PostAsJsonAsync("/api/discovery/tcp-scan",
                new { host = "127.0.0.1", authorizationToken = "t", ports = new[] { 80 } });
            Assert.AreEqual(HttpStatusCode.Forbidden, r1.StatusCode);

            using var lab = factory.CreateClient();
            lab.DefaultRequestHeaders.Add("X-Atlas-Role", "lab");
            var r2 = await lab.PostAsJsonAsync("/api/discovery/tcp-scan",
                new { host = "127.0.0.1", authorizationToken = "t", ports = new[] { 80 } });
            // Puede ser 200 o 400 (depende de si el host resuelve), pero NO 403
            Assert.AreNotEqual(HttpStatusCode.Forbidden, r2.StatusCode);
        }

        [TestMethod]
        public async Task Sandbox_publish_rechaza_sin_signalKey()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);
            var r = await client.PostAsJsonAsync("/api/sandbox/publish", new { signalKey = "", value = 1.0 });
            Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [TestMethod]
        public async Task Discovery_scan_sin_autorizacion_es_rechazado_aun_con_admin()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);
            var r = await client.PostAsJsonAsync("/api/discovery/tcp-scan",
                new { host = "127.0.0.1", authorizationToken = "" });
            Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [TestMethod]
        public async Task Admin_audit_lista_acciones_previas()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var client = AdminClient(factory);

            // Generar accion auditable
            var b = await client.PostAsync("/api/admin/backup", null);
            b.EnsureSuccessStatusCode();
            // Esperar flush del audit log
            await Task.Delay(200);

            var audit = await client.GetFromJsonAsync<JsonElement>("/api/admin/audit?max=10");
            var cnt = audit.GetProperty("count").GetInt32();
            if (cnt == 0)
            {
                var dbg = await client.GetStringAsync("/api/admin/audit?max=100");
                Assert.Fail($"audit vacio. Respuesta: {dbg}");
            }
            Assert.IsTrue(cnt >= 1, $"audit count={cnt}");
            var entries = audit.GetProperty("entries").EnumerateArray().ToList();
            Assert.IsTrue(entries.Any(e => e.GetProperty("action").GetString() == "backup.create"),
                "acciones presentes: " + string.Join(",", entries.Select(e => e.GetProperty("action").GetString())));
        }

        [TestMethod]
        public async Task Admin_audit_requiere_rol_admin()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var viewer = factory.CreateClient(); // sin rol
            var r = await viewer.GetAsync("/api/admin/audit");
            Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
        }
        [TestMethod]
        public async Task Denegacion_se_audita_en_signed_log()
        {
            var (factory, dataRoot) = CreateFactory();
            using var _f = factory;

            // Viewer intenta escribir → 403 auditado
            using var viewer = factory.CreateClient();
            var r = await viewer.PostAsJsonAsync("/api/assets", new { name = "ilegal" });
            Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);

            await Task.Delay(150); // dejar al middleware flushear

            using var admin = AdminClient(factory);
            var audit = await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit?action=http.request.denied");
            Assert.IsTrue(audit.GetProperty("count").GetInt32() >= 1,
                "la denegacion debio quedar registrada");
        }

        [TestMethod]
        public async Task Audit_verify_detecta_tampering_manual()
        {
            var (factory, dataRoot) = CreateFactory();
            using var _f = factory;
            using var admin = AdminClient(factory);

            // Generar algo de actividad
            var bk = await admin.PostAsync("/api/admin/backup", null);
            bk.EnsureSuccessStatusCode();

            // Verificar integridad base
            var okVerify = await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit/verify");
            Assert.IsTrue(okVerify.GetProperty("valid").GetBoolean(), "cadena debe ser valida antes del tamper");

            // Atacante altera el archivo directamente
            var logPath = Path.Combine(dataRoot, "audit", "audit-signed.log");
            Assert.IsTrue(File.Exists(logPath), "log debe existir");
            var content = await File.ReadAllTextAsync(logPath);
            // Cambiar un caracter cualquiera
            var idx = content.IndexOf("backup.create", StringComparison.Ordinal);
            Assert.IsTrue(idx > 0, "no encontre backup.create en el log");
            var tampered = content.Substring(0, idx) + "respaldo.crear" + content.Substring(idx + "backup.create".Length);
            await File.WriteAllTextAsync(logPath, tampered);

            // Verificar denuncia tamper
            var badVerify = await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit/verify");
            Assert.IsFalse(badVerify.GetProperty("valid").GetBoolean(), "tamper debio ser detectado");
            Assert.IsTrue(badVerify.TryGetProperty("firstTamperedEntryId", out _),
                "se debe reportar el id de la entrada dañada");
        }

        [TestMethod]
        public async Task Audit_filtra_por_severidad()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;
            using var admin = AdminClient(factory);

            // backup → Critical
            await admin.PostAsync("/api/admin/backup", null);
            // viewer 403 → Warning
            using var viewer = factory.CreateClient();
            await viewer.PostAsJsonAsync("/api/assets", new { name = "ilegal" });
            await Task.Delay(150);

            var critical = await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit?severity=critical");
            var warning = await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit?severity=warning");
            Assert.IsTrue(critical.GetProperty("count").GetInt32() >= 1);
            Assert.IsTrue(warning.GetProperty("count").GetInt32() >= 1);
            // Y las acciones difieren
            var critActions = critical.GetProperty("entries").EnumerateArray()
                .Select(e => e.GetProperty("action").GetString()).ToHashSet();
            var warnActions = warning.GetProperty("entries").EnumerateArray()
                .Select(e => e.GetProperty("action").GetString()).ToHashSet();
            Assert.IsTrue(critActions.Contains("backup.create"));
            Assert.IsTrue(warnActions.Contains("http.request.denied"));
        }

        [TestMethod]
        public async Task Correlator_rafaga_denegaciones_dispara_alarma()
        {
            var (factory, _) = CreateFactory();
            using var _f = factory;

            using var viewer = factory.CreateClient();
            // 12 denegaciones rapidas desde el mismo "cliente" (IP sera null en TestServer,
            // pero comparten ip="unknown" entonces se acumulan igual).
            for (int i = 0; i < 12; i++)
            {
                var r = await viewer.PostAsJsonAsync("/api/assets", new { name = $"ilegal-{i}" });
                Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
            }
            await Task.Delay(300); // darle al middleware tiempo de procesar la correlacion

            using var admin = AdminClient(factory);
            var alarmsResp = await admin.GetAsync("/api/alarms/active");
            alarmsResp.EnsureSuccessStatusCode();
            var alarms = await alarmsResp.Content.ReadFromJsonAsync<JsonElement>();
            var alarmList = alarms.EnumerateArray().ToList();
            Assert.IsTrue(alarmList.Count >= 1,
                $"se esperaba alarma por rafaga de denegaciones; activas={alarmList.Count}");
            var hasRate = alarmList.Any(a =>
            {
                var name = a.GetProperty("ruleName").GetString() ?? "";
                var key = a.GetProperty("signalKey").GetString() ?? "";
                return name.Contains("Denegaciones") || key.StartsWith("http.denied.rate");
            });
            Assert.IsTrue(hasRate, "ninguna alarma menciona la rafaga de denegaciones");
        }
    }
}
