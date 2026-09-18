using System;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AtlasGT.Api.Controllers
{
    /// <summary>
    /// Sandbox E2E: publica una Observation al stream SignalR sin hardware.
    /// Marcada SIMULATED explicitamente en su provenance.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SandboxController : ControllerBase
    {
        private readonly AtlasGT.Api.Services.ObservationStreamer _streamer;
        private readonly ConfigStore _store;

        public SandboxController(AtlasGT.Api.Services.ObservationStreamer streamer, ConfigStore store)
        {
            _streamer = streamer;
            _store = store;
        }

        public sealed class PublishRequest
        {
            public Guid? EndpointId { get; set; }
            public string SignalKey { get; set; } = string.Empty;
            public double? Value { get; set; }
            public string? ValueText { get; set; }
            public string? Unit { get; set; }
        }

        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] PublishRequest req, CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.SignalKey))
                return BadRequest(new { error = "SignalKey requerido" });

            var endpointId = req.EndpointId ?? Guid.Empty;
            // Verificar endpoint si viene especificado
            if (req.EndpointId.HasValue)
            {
                var snap = await _store.LoadAsync(ct);
                var exists = snap.Endpoints.Exists(e => e.Id == req.EndpointId.Value);
                if (!exists) return NotFound(new { error = "EndpointId no existe" });
            }

            var obs = new Observation
            {
                Id = Guid.NewGuid(),
                EndpointId = endpointId == Guid.Empty ? null : endpointId,
                Name = req.SignalKey,
                Description = req.ValueText ?? string.Empty,
                Value = req.Value,
                Unit = req.Unit ?? string.Empty,
                Transport = "sandbox",
                TrustTier = TrustTier.Observed,
                ReceivedAtUtc = DateTimeOffset.UtcNow
            };

            await _streamer.PublishAsync(obs, ct);
            return Accepted(new { observation = obs, mode = "SIMULATED" });
        }
    }
}
