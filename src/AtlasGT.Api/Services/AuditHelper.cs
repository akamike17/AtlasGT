using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace AtlasGT.Api.Services
{
    /// <summary>
    /// Helper para audit desde controllers: serializa before/after, sanitiza y persiste.
    /// </summary>
    public sealed class AuditHelper
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

        private readonly ISignedAuditLog _audit;
        private readonly IHttpContextAccessor _http;

        public AuditHelper(ISignedAuditLog audit, IHttpContextAccessor http)
        {
            _audit = audit;
            _http = http;
        }

        /// <summary>Registra una accion CRUD con before/after.</summary>
        public async Task RecordChangeAsync(
            string action,
            string? targetId,
            object? before,
            object? after,
            AuditSeverity severity = AuditSeverity.Info,
            CancellationToken ct = default)
        {
            try
            {
                var ctx = _http.HttpContext;
                var role = ctx?.Request.Headers["X-Atlas-Role"].FirstOrDefault() ?? "unknown";
                await _audit.AppendAsync(new SignedAuditEntry
                {
                    Actor = $"role:{role}",
                    Action = action,
                    TargetId = targetId,
                    Succeeded = true,
                    Severity = severity,
                    SourceIp = ctx?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = Truncate(ctx?.Request.Headers.UserAgent.ToString(), 256),
                    CorrelationId = ctx?.TraceIdentifier,
                    PayloadBefore = AuditSanitizer.Sanitize(before is null ? null : JsonSerializer.Serialize(before, JsonOpts)),
                    PayloadAfter = AuditSanitizer.Sanitize(after is null ? null : JsonSerializer.Serialize(after, JsonOpts))
                }, ct);
            }
            catch { /* nunca tumbar por audit */ }
        }

        public async Task RecordActionAsync(
            string action,
            string? targetId,
            bool succeeded,
            string? error = null,
            AuditSeverity severity = AuditSeverity.Info,
            CancellationToken ct = default)
        {
            try
            {
                var ctx = _http.HttpContext;
                var role = ctx?.Request.Headers["X-Atlas-Role"].FirstOrDefault() ?? "unknown";
                await _audit.AppendAsync(new SignedAuditEntry
                {
                    Actor = $"role:{role}",
                    Action = action,
                    TargetId = targetId,
                    Succeeded = succeeded,
                    ErrorMessage = error,
                    Severity = severity,
                    SourceIp = ctx?.Connection?.RemoteIpAddress?.ToString(),
                    UserAgent = Truncate(ctx?.Request.Headers.UserAgent.ToString(), 256),
                    CorrelationId = ctx?.TraceIdentifier
                }, ct);
            }
            catch { }
        }

        private static string? Truncate(string? s, int max)
            => s is null ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}
