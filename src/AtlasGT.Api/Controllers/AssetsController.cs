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
    /// <summary>CRUD de Assets (maquinas) sobre ConfigStore persistente.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AssetsController : ControllerBase
    {
        private readonly ConfigStore _store;
        private readonly AtlasGT.Api.Services.AuditHelper _audit;

        public AssetsController(ConfigStore store, AtlasGT.Api.Services.AuditHelper audit)
        {
            _store = store;
            _audit = audit;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<Asset>>> GetAll(CancellationToken ct)
        {
            var snap = await _store.LoadAsync(ct);
            return Ok(snap.Assets);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Asset>> GetById(Guid id, CancellationToken ct)
        {
            var snap = await _store.LoadAsync(ct);
            var a = snap.Assets.FirstOrDefault(x => x.Id == id);
            return a is null ? NotFound() : Ok(a);
        }

        [HttpPost]
        public async Task<ActionResult<Asset>> Create([FromBody] Asset model, CancellationToken ct)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { error = "Name requerido" });
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            await _store.MutateAsync<object?>(snap => { snap.Assets.Add(model); return null; }, ct);
            await _audit.RecordChangeAsync("asset.create", model.Id.ToString(), before: null, after: model, ct: ct);
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Asset>> Update(Guid id, [FromBody] Asset model, CancellationToken ct)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { error = "Name requerido" });
            Asset? before = null;
            var result = await _store.MutateAsync<Asset?>(snap =>
            {
                var a = snap.Assets.FirstOrDefault(x => x.Id == id);
                if (a is null) return null;
                before = new Asset { Id = a.Id, Name = a.Name, Tag = a.Tag, Description = a.Description, AreaId = a.AreaId };
                a.Name = model.Name;
                a.Description = model.Description;
                a.Tag = model.Tag;
                a.AreaId = model.AreaId;
                a.UpdatedAt = DateTime.UtcNow;
                return a;
            }, ct);
            if (result is not null)
                await _audit.RecordChangeAsync("asset.update", id.ToString(), before, result, ct: ct);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            Asset? removed = null;
            var removedCount = await _store.MutateAsync(snap =>
            {
                removed = snap.Assets.FirstOrDefault(a => a.Id == id);
                return snap.Assets.RemoveAll(a => a.Id == id);
            }, ct);
            if (removedCount > 0)
                await _audit.RecordChangeAsync("asset.delete", id.ToString(), removed, after: null, severity: AuditSeverity.Warning, ct: ct);
            return removedCount > 0 ? NoContent() : NotFound();
        }

        /// <summary>Estado accionable para operador: nombre + ultimos signals + salud.</summary>
        [HttpGet("{id:guid}/dashboard")]
        public async Task<ActionResult<object>> Dashboard(Guid id, CancellationToken ct)
        {
            var snap = await _store.LoadAsync(ct);
            var a = snap.Assets.FirstOrDefault(x => x.Id == id);
            if (a is null) return NotFound();
            return Ok(new
            {
                asset = a,
                endpoints = snap.Endpoints.Where(e =>
                    a.Interfaces.Any(i => i.Id == e.InterfaceId)).ToList(),
                signals = a.Signals,
                activeAlarms = Array.Empty<object>() // poblado por AlarmEngine en vivo via SignalR
            });
        }
    }
}
