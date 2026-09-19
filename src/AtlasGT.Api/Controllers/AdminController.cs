using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AtlasGT.Api.Controllers
{
    /// <summary>Endpoints de administracion: backup, restore, lista de backups, auditoria firmada.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IBackupService _backup;
        private readonly ConfigStore _config;
        private readonly ISignedAuditLog _audit;

        public AdminController(IBackupService backup, ConfigStore config, ISignedAuditLog audit)
        {
            _backup = backup;
            _config = config;
            _audit = audit;
        }

        /// <summary>Consulta audit log con filtros.</summary>
        [HttpGet("audit")]
        public async Task<IActionResult> GetAudit(
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] string? actor = null,
            [FromQuery] string? action = null,
            [FromQuery] string? severity = null,
            [FromQuery] int max = 100,
            CancellationToken ct = default)
        {
            AuditSeverity? sev = null;
            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<AuditSeverity>(severity, true, out var s))
                sev = s;

            var entries = await _audit.ReadAsync(from, to, actor, action, sev, max, ct);
            return Ok(new { count = entries.Count, entries });
        }

        /// <summary>Verifica la integridad de la cadena completa. Solo admin.</summary>
        [HttpGet("audit/verify")]
        public async Task<IActionResult> VerifyAudit(CancellationToken ct)
        {
            var result = await _audit.VerifyAsync(ct);
            return Ok(new
            {
                valid = result.Valid,
                totalEntries = result.TotalEntries,
                firstTamperedEntryId = result.FirstTamperedEntryId,
                tamperReason = result.TamperReason,
                lastHash = result.LastHash
            });
        }

        /// <summary>
        /// Export CSV del audit log con manifiesto SHA-256 al final.
        /// El CSV incluye columna EntryHash y PrevHash para verificacion offline.
        /// Termina con una linea `MANIFEST,<sha256-del-csv-sin-manifiesto>,<entries>` que
        /// permite detectar cualquier alteracion post-export.
        /// </summary>
        [HttpGet("audit/export.csv")]
        public async Task<IActionResult> ExportAuditCsv(CancellationToken ct)
        {
            // ReadAllAsync: sin cap de 1000. El audit log entra en memoria; eso
            // es intencional — export debe reflejar TODO el log, no una vista
            // truncada silenciosamente.
            var entries = await _audit.ReadAllAsync(ct: ct);
            var chrono = entries.OrderBy(e => e.AtUtc).ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("id,atUtc,actor,action,targetId,succeeded,severity,sourceIp,userAgent,correlationId,errorMessage,entryHash,prevHash");
            foreach (var e in chrono)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(e.Id.ToString()),
                    Csv(e.AtUtc.ToString("O")),
                    Csv(e.Actor),
                    Csv(e.Action),
                    Csv(e.TargetId),
                    e.Succeeded ? "1" : "0",
                    Csv(e.Severity.ToString()),
                    Csv(e.SourceIp),
                    Csv(e.UserAgent),
                    Csv(e.CorrelationId),
                    Csv(e.ErrorMessage),
                    Csv(e.EntryHash),
                    Csv(e.PrevHash)
                }));
            }

            // Manifiesto: hash del contenido hasta aqui (sin la linea MANIFEST)
            var csvBody = sb.ToString();
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(csvBody);
            var hash = System.Security.Cryptography.SHA256.HashData(bodyBytes);
            var manifestLine = $"MANIFEST,{Convert.ToHexString(hash).ToLowerInvariant()},{chrono.Count}";
            sb.AppendLine(manifestLine);

            await _audit.AppendAsync(new SignedAuditEntry
            {
                Actor = "role:" + (Request.Headers["X-Atlas-Role"].FirstOrDefault() ?? "unknown"),
                Action = "audit.export",
                Detail = $"entries={chrono.Count}",
                Succeeded = true,
                Severity = AuditSeverity.Warning,
                SourceIp = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                CorrelationId = HttpContext?.TraceIdentifier
            }, ct);

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fname = $"atlas-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(bytes, "text/csv", fname);
        }

        private static string Csv(string? s)
        {
            if (s is null) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        [HttpPost("backup")]
        public async Task<IActionResult> CreateBackup(CancellationToken ct)
        {
            try
            {
                var path = await _backup.CreateBackupAsync(null, ct);
                await Audit("backup.create", path, true, null, AuditSeverity.Critical, ct);
                return Ok(new { path, sizeBytes = new System.IO.FileInfo(path).Length });
            }
            catch (Exception ex)
            {
                await Audit("backup.create", null, false, ex.Message, AuditSeverity.Critical, ct);
                var src = (_backup as AtlasGT.Infrastructure.FileBackupService)?.SourceDirForDebug;
                return StatusCode(500, new
                {
                    error = ex.Message,
                    exceptionType = ex.GetType().Name,
                    sourceDir = src,
                    hint = "revisar logs del proceso y audit log"
                });
            }
        }

        [HttpGet("backups")]
        public async Task<IActionResult> ListBackups(CancellationToken ct)
        {
            var list = await _backup.ListBackupsAsync(null, ct);
            return Ok(list.Select(p => new { path = p, sizeBytes = new System.IO.FileInfo(p).Length }));
        }

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] RestoreRequest req, CancellationToken ct)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.ZipPath))
                return BadRequest(new { error = "ZipPath requerido" });
            var ok = await _backup.RestoreAsync(req.ZipPath, null, ct);
            await Audit("backup.restore", req.ZipPath, ok, ok ? null : "restore fallo", AuditSeverity.Critical, ct);
            return ok ? Ok(new { restored = req.ZipPath }) : NotFound(new { error = "zip no encontrado o corrupto" });
        }

        private async Task Audit(string action, string? target, bool ok, string? error, AuditSeverity sev, CancellationToken ct)
        {
            try
            {
                var role = Request?.Headers["X-Atlas-Role"].FirstOrDefault() ?? "unknown";
                await _audit.AppendAsync(new SignedAuditEntry
                {
                    Actor = $"role:{role}",
                    Action = action,
                    TargetId = target,
                    Succeeded = ok,
                    ErrorMessage = error,
                    Severity = sev,
                    SourceIp = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = Request?.Headers.UserAgent.ToString() is { Length: > 256 } ua ? ua.Substring(0, 256) : Request?.Headers.UserAgent.ToString(),
                    CorrelationId = HttpContext?.TraceIdentifier
                }, ct);
            }
            catch { /* nunca tumbar por audit */ }
        }
    }

    public sealed class RestoreRequest
    {
        public string ZipPath { get; set; } = string.Empty;
    }
}
