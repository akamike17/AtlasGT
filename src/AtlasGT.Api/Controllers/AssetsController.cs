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

        public AssetsController(ConfigStore store) => _store = store;

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
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<Asset>> Update(Guid id, [FromBody] Asset model, CancellationToken ct)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { error = "Name requerido" });
            var result = await _store.MutateAsync<Asset?>(snap =>
            {
                var a = snap.Assets.FirstOrDefault(x => x.Id == id);
                if (a is null) return null;
                a.Name = model.Name;
                a.Description = model.Description;
                a.Tag = model.Tag;
                a.AreaId = model.AreaId;
                a.UpdatedAt = DateTime.UtcNow;
                return a;
            }, ct);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var removed = await _store.MutateAsync(snap => snap.Assets.RemoveAll(a => a.Id == id), ct);
            return removed > 0 ? NoContent() : NotFound();
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
