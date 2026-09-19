using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Infrastructure
{
    /// <summary>
    /// SignedFileAuditLog: audit log append-only con firma SHA-256 encadenada.
    ///
    /// Cada linea es un SignedAuditEntry en JSON. El EntryHash cubre los campos
    /// canonicos + PrevHash (el hash de la linea anterior). Alterar, borrar o
    /// reordenar cualquier linea quiebra la cadena.
    ///
    /// Limitaciones honestas:
    /// - No es WORM (cualquier proceso FS puede tocar el archivo); la firma
    ///   lo que garantiza es que *cualquier modificacion se detecta*.
    /// - La raiz de confianza (primer GENESIS) esta implicita. En produccion,
    ///   conviene anclar (anchor) el ultimo hash en lugar externo cada N minutos.
    /// </summary>
    public interface ISignedAuditLog
    {
        Task<SignedAuditEntry> AppendAsync(SignedAuditEntry entry, CancellationToken ct = default);
        Task<AuditChainVerification> VerifyAsync(CancellationToken ct = default);
        Task<IReadOnlyList<SignedAuditEntry>> ReadAsync(
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            string? actor = null,
            string? action = null,
            AuditSeverity? severity = null,
            int limit = 100,
            CancellationToken ct = default);

        /// <summary>
        /// Lectura sin limite para exports/forense. Puede ser costoso en memoria
        /// si el log es enorme; preferible a truncar silenciosamente.
        /// </summary>
        Task<IReadOnlyList<SignedAuditEntry>> ReadAllAsync(
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            string? actor = null,
            string? action = null,
            AuditSeverity? severity = null,
            CancellationToken ct = default);
    }

    public sealed class AuditChainVerification
    {
        public bool Valid { get; set; }
        public int TotalEntries { get; set; }
        public string? FirstTamperedEntryId { get; set; }
        public string? TamperReason { get; set; }
        public string? LastHash { get; set; }
    }

    public sealed class SignedFileAuditLog : ISignedAuditLog
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _file;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public SignedFileAuditLog(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir)) throw new ArgumentException("baseDir requerido");
            Directory.CreateDirectory(baseDir);
            _file = Path.Combine(baseDir, "audit-signed.log");
        }

        public string FilePath => _file;

        public async Task<SignedAuditEntry> AppendAsync(SignedAuditEntry entry, CancellationToken ct = default)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Encontrar ultima hash → PrevHash
                var lastHash = await ReadLastHashInternalAsync(ct).ConfigureAwait(false);
                entry.PrevHash = lastHash ?? "GENESIS";
                entry.ComputeAndSetHash();

                var line = JsonSerializer.Serialize(entry, Json) + "\n";
                await File.AppendAllTextAsync(_file, line, Encoding.UTF8, ct).ConfigureAwait(false);
                return entry;
            }
            finally { _gate.Release(); }
        }

        public async Task<AuditChainVerification> VerifyAsync(CancellationToken ct = default)
        {
            var result = new AuditChainVerification();
            if (!File.Exists(_file))
            {
                result.Valid = true;
                return result;
            }

            string expectedPrev = "GENESIS";
            await foreach (var (line, lineNo) in ReadLinesAsync(ct).ConfigureAwait(false))
            {
                SignedAuditEntry? entry = null;
                try { entry = JsonSerializer.Deserialize<SignedAuditEntry>(line, Json); }
                catch (JsonException)
                {
                    result.Valid = false;
                    result.TamperReason = $"linea {lineNo} no es JSON valido";
                    return result;
                }
                if (entry is null)
                {
                    result.Valid = false;
                    result.TamperReason = $"linea {lineNo} deserializa a null";
                    return result;
                }

                result.TotalEntries++;

                // 1. PrevHash consistente con la entrada anterior
                if (!string.Equals(entry.PrevHash, expectedPrev, StringComparison.OrdinalIgnoreCase))
                {
                    result.Valid = false;
                    result.FirstTamperedEntryId = entry.Id.ToString();
                    result.TamperReason = $"linea {lineNo}: PrevHash no coincide con la entrada anterior";
                    return result;
                }

                // 2. EntryHash correcto
                if (!entry.VerifySelf())
                {
                    result.Valid = false;
                    result.FirstTamperedEntryId = entry.Id.ToString();
                    result.TamperReason = $"linea {lineNo}: EntryHash invalido (contenido alterado)";
                    return result;
                }

                expectedPrev = entry.EntryHash;
                result.LastHash = entry.EntryHash;
            }

            result.Valid = true;
            return result;
        }

        public async Task<IReadOnlyList<SignedAuditEntry>> ReadAsync(
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            string? actor = null,
            string? action = null,
            AuditSeverity? severity = null,
            int limit = 100,
            CancellationToken ct = default)
        {
            var results = new List<SignedAuditEntry>();
            if (!File.Exists(_file)) return results;

            await foreach (var (line, _) in ReadLinesAsync(ct).ConfigureAwait(false))
            {
                SignedAuditEntry? entry;
                try { entry = JsonSerializer.Deserialize<SignedAuditEntry>(line, Json); }
                catch (JsonException) { continue; }
                if (entry is null) continue;

                if (fromUtc.HasValue && entry.AtUtc < fromUtc.Value) continue;
                if (toUtc.HasValue && entry.AtUtc > toUtc.Value) continue;
                if (!string.IsNullOrWhiteSpace(actor) &&
                    !entry.Actor.Contains(actor, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(action) &&
                    !string.Equals(entry.Action, action, StringComparison.OrdinalIgnoreCase)) continue;
                if (severity.HasValue && entry.Severity != severity.Value) continue;

                results.Add(entry);
            }

            // Devolver mas recientes primero, con limite
            return results
                .OrderByDescending(e => e.AtUtc)
                .Take(Math.Clamp(limit, 1, 1000))
                .ToList();
        }

        public async Task<IReadOnlyList<SignedAuditEntry>> ReadAllAsync(
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            string? actor = null,
            string? action = null,
            AuditSeverity? severity = null,
            CancellationToken ct = default)
        {
            var results = new List<SignedAuditEntry>();
            if (!File.Exists(_file)) return results;

            await foreach (var (line, _) in ReadLinesAsync(ct).ConfigureAwait(false))
            {
                SignedAuditEntry? entry;
                try { entry = JsonSerializer.Deserialize<SignedAuditEntry>(line, Json); }
                catch (JsonException) { continue; }
                if (entry is null) continue;

                if (fromUtc.HasValue && entry.AtUtc < fromUtc.Value) continue;
                if (toUtc.HasValue && entry.AtUtc > toUtc.Value) continue;
                if (!string.IsNullOrWhiteSpace(actor) &&
                    !entry.Actor.Contains(actor, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(action) &&
                    !string.Equals(entry.Action, action, StringComparison.OrdinalIgnoreCase)) continue;
                if (severity.HasValue && entry.Severity != severity.Value) continue;

                results.Add(entry);
            }
            // Cronologico inverso, SIN cap. Usuario asume el costo de memoria.
            return results.OrderByDescending(e => e.AtUtc).ToList();
        }

        private async Task<string?> ReadLastHashInternalAsync(CancellationToken ct)
        {
            if (!File.Exists(_file)) return null;
            string? lastHash = null;
            await foreach (var (line, _) in ReadLinesAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    var e = JsonSerializer.Deserialize<SignedAuditEntry>(line, Json);
                    if (e is not null && !string.IsNullOrEmpty(e.EntryHash))
                        lastHash = e.EntryHash;
                }
                catch (JsonException) { /* ignorar */ }
            }
            return lastHash;
        }

        private async IAsyncEnumerable<(string Line, int LineNo)> ReadLinesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            using var sr = new StreamReader(_file, Encoding.UTF8);
            string? line;
            int n = 0;
            while ((line = await sr.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;
                n++;
                yield return (line, n);
            }
        }
    }
}
