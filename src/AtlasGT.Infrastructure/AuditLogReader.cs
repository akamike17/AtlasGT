using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Infrastructure
{
    /// <summary>Extension de FileAuditLog con lectura para el endpoint /api/admin/audit.</summary>
    public static class AuditLogReader
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            // Sin PropertyNamingPolicy: FileAuditLog escribe con propiedades PascalCase
            // (defaults de .NET), asi que leemos igual. Son ambos contractos "internos".
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Lee las ultimas N entradas del audit log. Devuelve mas recientes primero.</summary>
        public static async Task<IReadOnlyList<AuditEntry>> ReadLatestAsync(
            string filePath, int max = 100, CancellationToken ct = default)
        {
            var result = new List<AuditEntry>();
            if (!File.Exists(filePath)) return result;

            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
            // Recorrer del final hacia atras
            for (int i = lines.Length - 1; i >= 0 && result.Count < max; i--)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var e = JsonSerializer.Deserialize<AuditEntry>(line, JsonOpts);
                    if (e != null) result.Add(e);
                }
                catch (JsonException) { /* linea corrupta: saltar */ }
            }
            return result;
        }
    }
}
