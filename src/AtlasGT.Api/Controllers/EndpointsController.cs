using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Application;
using AtlasGT.Domain.Models;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using DomainEndpoint = AtlasGT.Domain.Models.Endpoint;

namespace AtlasGT.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EndpointsController : ControllerBase
    {
        private readonly ConfigStore _store;
        private readonly IDoctorService _doctor;

        public EndpointsController(ConfigStore store, IDoctorService doctor)
        {
            _store = store;
            _doctor = doctor;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DomainEndpoint>>> GetAll(CancellationToken ct)
            => Ok((await _store.LoadAsync(ct)).Endpoints);

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DomainEndpoint>> GetById(Guid id, CancellationToken ct)
        {
            var ep = (await _store.LoadAsync(ct)).Endpoints.FirstOrDefault(e => e.Id == id);
            return ep is null ? NotFound() : Ok(ep);
        }

        [HttpGet("{id:guid}/diagnose")]
        public async Task<ActionResult<object>> Diagnose(Guid id, CancellationToken ct)
        {
            var ep = (await _store.LoadAsync(ct)).Endpoints.FirstOrDefault(e => e.Id == id);
            if (ep is null) return NotFound();
            return Ok(_doctor.Diagnose(ep));
        }

        [HttpPost]
        public async Task<ActionResult<DomainEndpoint>> Create([FromBody] DomainEndpoint model, CancellationToken ct)
        {
            if (model is null || string.IsNullOrWhiteSpace(model.Address))
                return BadRequest(new { error = "Address requerida" });
            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            await _store.MutateAsync<object?>(snap => { snap.Endpoints.Add(model); return null; }, ct);
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var removed = await _store.MutateAsync(snap => snap.Endpoints.RemoveAll(e => e.Id == id), ct);
            return removed > 0 ? NoContent() : NotFound();
        }

        /// <summary>
        /// Tocar el endpoint marca actividad observada (para simular hardware observado en sandbox).
        /// Passive-only: no envia comandos.
        /// </summary>
        [HttpPost("{id:guid}/touch")]
        public async Task<IActionResult> Touch(Guid id, CancellationToken ct)
        {
            var result = await _store.MutateAsync<DomainEndpoint?>(snap =>
            {
                var ep = snap.Endpoints.FirstOrDefault(e => e.Id == id);
                if (ep is null) return null;
                ep.LastObservedAtUtc = DateTime.UtcNow;
                if (ep.TrustTier < TrustTier.Observed) ep.TrustTier = TrustTier.Observed;
                ep.UpdatedAt = DateTime.UtcNow;
                return ep;
            }, ct);
            return result is null ? NotFound() : Ok(result);
        }
    }
}
