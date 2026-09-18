using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
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
    /// </summary>
    [TestClass]
    public class ApiSmokeTests
    {
        private static WebApplicationFactory<Program> CreateFactory()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "atlas_api_test_" + Guid.NewGuid().ToString("N"));
            return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("AtlasGT:ConfigPath", Path.Combine(tmp, "config.json"))
                    });
                });
            });
        }

        [TestMethod]
        public async Task Health_responde_ok()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();
            var r = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
        }

        [TestMethod]
        public async Task Assets_crud_fluye()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            // Crear
            var created = await client.PostAsJsonAsync("/api/assets", new { name = "PRENSA-04", tag = "PRENSA-04" });
            Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
            var asset = await created.Content.ReadFromJsonAsync<AtlasGT.Domain.Models.Asset>();
            Assert.IsNotNull(asset);
            Assert.AreNotEqual(Guid.Empty, asset!.Id);

            // Listar
            var list = await client.GetFromJsonAsync<AtlasGT.Domain.Models.Asset[]>("/api/assets");
            Assert.IsTrue(list!.Length >= 1);

            // GetById
            var fetched = await client.GetFromJsonAsync<AtlasGT.Domain.Models.Asset>($"/api/assets/{asset.Id}");
            Assert.AreEqual(asset.Name, fetched!.Name);

            // Delete
            var del = await client.DeleteAsync($"/api/assets/{asset.Id}");
            Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);
        }

        [TestMethod]
        public async Task Profiles_export_import_roundtrip()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            await client.PostAsJsonAsync("/api/profiles", new { manufacturer = "Acme", model = "X1", protocol = "HTTP" });
            var export = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/profiles/export");
            Assert.IsTrue(export.GetProperty("profiles").GetArrayLength() >= 1);

            // No debe re-importar duplicados (idempotente)
            var exportJson = await client.GetStringAsync("/api/profiles/export");
            var doc = JsonDocument.Parse(exportJson);
            var profilesJson = doc.RootElement.GetProperty("profiles").GetRawText();
            var impResp = await client.PostAsync("/api/profiles/import",
                new StringContent(profilesJson, System.Text.Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.OK, impResp.StatusCode,
                "Body: " + await impResp.Content.ReadAsStringAsync());
            var impBody = await impResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.AreEqual(0, impBody.GetProperty("imported").GetInt32());
        }

        [TestMethod]
        public async Task Alarm_rule_persiste_y_se_registra_en_engine()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();

            var post = await client.PostAsJsonAsync("/api/alarms/rules", new
            {
                name = "Temp alta",
                signalKey = "temp",
                threshold = 50.0,
                comparison = "GreaterThan",
                severity = "Warning"
            });
            Assert.AreEqual(HttpStatusCode.Created, post.StatusCode);

            var list = await client.GetFromJsonAsync<AtlasGT.Infrastructure.AlarmRuleDto[]>("/api/alarms/rules");
            Assert.IsTrue(list!.Length >= 1);
        }

        [TestMethod]
        public async Task Sandbox_publish_rechaza_sin_signalKey()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();
            var r = await client.PostAsJsonAsync("/api/sandbox/publish", new { signalKey = "", value = 1.0 });
            Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [TestMethod]
        public async Task Discovery_scan_requiere_autorizacion()
        {
            using var factory = CreateFactory();
            using var client = factory.CreateClient();
            var r = await client.PostAsJsonAsync("/api/discovery/tcp-scan", new { host = "127.0.0.1", authorizationToken = "" });
            Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
        }
    }
}
