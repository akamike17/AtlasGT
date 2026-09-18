using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;
using AtlasGT.Discovery;
using Microsoft.AspNetCore.Mvc;

namespace AtlasGT.Api.Controllers
{
    /// <summary>
    /// Discovery autorizado (sec. 12). Read-only, passive-first.
    /// Toda solicitud exige un token de autorizacion explicito en el body.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiscoveryController : ControllerBase
    {
        private readonly TcpPortDiscovery _tcp;

        public DiscoveryController(TcpPortDiscovery tcp) => _tcp = tcp;

        public sealed class ScanRequest
        {
            /// <summary>Obligatorio: cadena no-vacia indicando autorizacion explicita.</summary>
            public string AuthorizationToken { get; set; } = string.Empty;
            public string Host { get; set; } = string.Empty;
            public int[]? Ports { get; set; }
            public int TimeoutMs { get; set; } = 500;
        }

        [HttpPost("tcp-scan")]
        public async Task<IActionResult> TcpScan([FromBody] ScanRequest req, CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.AuthorizationToken))
                return BadRequest(new { error = "AuthorizationToken requerido (escaneo read-only pero debe ser autorizado)" });
            if (string.IsNullOrWhiteSpace(req.Host))
                return BadRequest(new { error = "Host requerido" });

            var ports = req.Ports is { Length: > 0 } ? req.Ports : new[] { 502, 4840, 1883, 80, 443 };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var results = new List<object>();
            foreach (var p in ports)
            {
                bool open;
                try { open = await _tcp.IsPortOpenAsync(req.Host, p, req.TimeoutMs, timeoutCts.Token); }
                catch (OperationCanceledException) { open = false; }
                results.Add(new { port = p, open, state = open ? "OPEN" : "CLOSED_OR_FILTERED" });
            }

            return Ok(new
            {
                host = req.Host,
                scannedAtUtc = DateTime.UtcNow,
                authorizationProvided = true,
                mode = "PASSIVE_READ_ONLY",
                results
            });
        }
    }
}
