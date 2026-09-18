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
    /// <summary>Endpoints de administracion: backup, restore, lista de backups, auditoria.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IBackupService _backup;
        private readonly ConfigStore _config;
        private readonly IAuditLog _audit;
        private readonly string _auditFilePath;

        public AdminController(IBackupService backup, ConfigStore config, IAuditLog audit, Microsoft.Extensions.Configuration.IConfiguration cfg)
        {
            _backup = backup;
            _config = config;
            _audit = audit;
            var root = cfg["AtlasGT:DataRoot"] ?? Path.Combine(AppContext.BaseDirectory, "data");
            _auditFilePath = Path.Combine(root, "audit", "audit.log");
        }

        /// <summary>Consulta el audit log (solo admin, por RoleGateMiddleware).</summary>
        [HttpGet("audit")]
        public async Task<IActionResult> GetAudit([FromQuery] int max = 100, CancellationToken ct = default)
        {
            var entries = await AuditLogReader.ReadLatestAsync(_auditFilePath, Math.Clamp(max, 1, 1000), ct);
            return Ok(new { count = entries.Count, entries });
        }

        [HttpPost("backup")]
        public async Task<IActionResult> CreateBackup(CancellationToken ct)
        {
            try
            {
                var path = await _backup.CreateBackupAsync(null, ct);
                await Audit("backup.create", path, true, null, ct);
                return Ok(new { path, sizeBytes = new System.IO.FileInfo(path).Length });
            }
            catch (Exception ex)
            {
                await Audit("backup.create", null, false, ex.Message, ct);
                // Incluir source y stack para diagnostico desde scripts (no exponer secretos porque path es local al server).
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
            await Audit("backup.restore", req.ZipPath, ok, ok ? null : "restore fallo", ct);
            return ok ? Ok(new { restored = req.ZipPath }) : NotFound(new { error = "zip no encontrado o corrupto" });
        }

        private async Task Audit(string action, string? target, bool ok, string? error, CancellationToken ct)
        {
            try
            {
                await _audit.RecordAsync(new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    AtUtc = DateTimeOffset.UtcNow,
                    Actor = User?.Identity?.Name ?? "anonymous",
                    Action = action,
                    TargetId = target,
                    Succeeded = ok,
                    ErrorMessage = error
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
