using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AtlasGT.Api.Controllers
{
    /// <summary>CRUD de Device Profiles import/export (sec. 11).</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProfilesController : ControllerBase
    {
        private readonly ConfigStore _store;
        private readonly AtlasGT.Api.Services.AuditHelper _audit;
        public ProfilesController(ConfigStore store, AtlasGT.Api.Services.AuditHelper audit)
        {
            _store = store;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DeviceProfile>>> GetAll(CancellationToken ct)
            => Ok((await _store.LoadAsync(ct)).Profiles);

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DeviceProfile>> GetById(Guid id, CancellationToken ct)
        {
            var p = (await _store.LoadAsync(ct)).Profiles.FirstOrDefault(x => x.Id == id);
            return p is null ? NotFound() : Ok(p);
        }

        [HttpPost]
        public async Task<ActionResult<DeviceProfile>> Create([FromBody] DeviceProfile model, CancellationToken ct)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Manufacturer) || string.IsNullOrWhiteSpace(model.Model))
                return BadRequest(new { error = "Manufacturer y Model requeridos" });
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedAt = DateTime.UtcNow;
            await _store.MutateAsync<object?>(snap => { snap.Profiles.Add(model); return null; }, ct);
            await _audit.RecordChangeAsync("profile.create", model.Id.ToString(), null, model, ct: ct);
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            DeviceProfile? removed = null;
            var n = await _store.MutateAsync(snap =>
            {
                removed = snap.Profiles.FirstOrDefault(p => p.Id == id);
                return snap.Profiles.RemoveAll(p => p.Id == id);
            }, ct);
            if (n > 0)
                await _audit.RecordChangeAsync("profile.delete", id.ToString(), removed, null, AuditSeverity.Warning, ct);
            return n > 0 ? NoContent() : NotFound();
        }

        /// <summary>Exportar todo el catalogo de perfiles como JSON (portable).</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(CancellationToken ct)
        {
            var snap = await _store.LoadAsync(ct);
            return Ok(new { schema = "atlasgt-profiles-v1", exportedAtUtc = DateTime.UtcNow, profiles = snap.Profiles });
        }

        /// <summary>Importacion bulk: valida Manufacturer/Model antes de guardar.</summary>
        [HttpPost("import")]
        public async Task<IActionResult> Import([FromBody] List<DeviceProfile> profiles, CancellationToken ct)
        {
            if (profiles is null || profiles.Count == 0)
                return BadRequest(new { error = "Lista vacia" });
            if (profiles.Any(p => string.IsNullOrWhiteSpace(p.Manufacturer) || string.IsNullOrWhiteSpace(p.Model)))
                return BadRequest(new { error = "Todo perfil requiere Manufacturer y Model" });

            var added = await _store.MutateAsync(snap =>
            {
                var n = 0;
                foreach (var p in profiles)
                {
                    if (snap.Profiles.Any(x => x.Id == p.Id)) continue; // idempotente
                    if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();
                    snap.Profiles.Add(p);
                    n++;
                }
                return n;
            }, ct);
            await _audit.RecordActionAsync("profile.import", null, true, null,
                added > 0 ? AuditSeverity.Warning : AuditSeverity.Info, ct);
            return Ok(new { imported = added });
        }
    }
}
