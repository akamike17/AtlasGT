using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Infrastructure
{
    /// <summary>
    /// Audit log append-only. Registra: quien, cuando, que, que cambio, antes/despues,
    /// comando, resultado, correlation id (spec sec. 23).
    /// </summary>
    public interface IAuditLog
    {
        Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    }

    public sealed class AuditEntry
    {
        public required Guid Id { get; init; }
        public required DateTimeOffset AtUtc { get; init; }
        public required string Actor { get; init; }       // "system", "user:admin", ...
        public required string Action { get; init; }      // "endpoint.authorize", "profile.import", ...
        public string? TargetId { get; init; }
        public string? Detail { get; init; }
        public string? CorrelationId { get; init; }
        public bool Succeeded { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public sealed class FileAuditLog : IAuditLog
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        private readonly string _file;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public FileAuditLog(string baseDir)
        {
            Directory.CreateDirectory(baseDir);
            _file = Path.Combine(baseDir, "audit.log");
        }

        public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));
            var line = JsonSerializer.Serialize(entry, Json) + "\n";
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(_file, line, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }
    }
}
