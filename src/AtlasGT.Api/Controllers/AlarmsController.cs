using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Application.Alarms;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AtlasGT.Api.Controllers
{
    /// <summary>CRUD de reglas de alarma + acciones sobre instancias activas (ack/clear).</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AlarmsController : ControllerBase
    {
        private readonly ConfigStore _store;
        private readonly AlarmEngine _engine;

        public AlarmsController(ConfigStore store, AlarmEngine engine)
        {
            _store = store;
            _engine = engine;
        }

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules(CancellationToken ct)
            => Ok((await _store.LoadAsync(ct)).AlarmRules);

        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] AlarmRuleDto rule, CancellationToken ct)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.SignalKey))
                return BadRequest(new { error = "SignalKey requerido" });
            if (rule.Id == Guid.Empty) rule.Id = Guid.NewGuid();
            await _store.MutateAsync<object?>(snap => { snap.AlarmRules.Add(rule); return null; }, ct);
            SyncEngine(rule);
            return CreatedAtAction(nameof(GetRules), new { id = rule.Id }, rule);
        }

        [HttpDelete("rules/{id:guid}")]
        public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
        {
            var removed = await _store.MutateAsync(snap => snap.AlarmRules.RemoveAll(r => r.Id == id), ct);
            return removed > 0 ? NoContent() : NotFound();
        }

        [HttpGet("active")]
        public IActionResult Active() => Ok(_engine.ActiveAlarms);

        [HttpPost("{id:guid}/ack")]
        public IActionResult Ack(Guid id, [FromBody] AckRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Actor))
                return BadRequest(new { error = "Actor requerido" });
            return _engine.Acknowledge(id, req.Actor) ? Ok() : NotFound();
        }

        [HttpPost("{id:guid}/clear")]
        public IActionResult Clear(Guid id, [FromBody] ClearRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Reason))
                return BadRequest(new { error = "Reason requerido" });
            return _engine.Clear(id, req.Reason) ? Ok() : NotFound();
        }

        private void SyncEngine(AlarmRuleDto dto)
        {
            if (!Enum.TryParse<AlarmComparison>(dto.Comparison, true, out var cmp)) cmp = AlarmComparison.GreaterThan;
            if (!Enum.TryParse<AtlasGT.Domain.Models.AlarmSeverity>(dto.Severity, true, out var sev)) sev = AtlasGT.Domain.Models.AlarmSeverity.Warning;
            _engine.AddRule(new AlarmRule
            {
                Id = dto.Id,
                Name = dto.Name,
                SignalKey = dto.SignalKey,
                Threshold = dto.Threshold,
                Comparison = cmp,
                Severity = sev,
                Debounce = dto.Debounce,
                MaxPerMinute = dto.MaxPerMinute
            });
        }

        public sealed class AckRequest { public string Actor { get; set; } = string.Empty; }
        public sealed class ClearRequest { public string Reason { get; set; } = string.Empty; }
    }
}
