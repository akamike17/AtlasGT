using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AtlasGT.Infrastructure
{
    /// <summary>Severidad de un evento de auditoria.</summary>
    public enum AuditSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    /// <summary>
    /// Entrada de auditoria firmada. El EntryHash cubre los campos canonicos
    /// mas PrevHash — cualquier alteracion rompe la cadena.
    /// </summary>
    public sealed class SignedAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>"user:admin" | "api_key:XYZ..." | "system" | "viewer:192.168.1.5"</summary>
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? TargetId { get; set; }

        /// <summary>"result=success|deny" y metadata relevante. Sanitizado.</summary>
        public string? Detail { get; set; }

        public string? CorrelationId { get; set; }
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>IP origen (tal cual llega, antes de cualquier proxy interno).</summary>
        public string? SourceIp { get; set; }

        /// <summary>User-Agent (truncado a 256 chars).</summary>
        public string? UserAgent { get; set; }

        public AuditSeverity Severity { get; set; } = AuditSeverity.Info;

        /// <summary>Estado previo (JSON, sanitizado). Null si no aplica.</summary>
        public string? PayloadBefore { get; set; }

        /// <summary>Estado nuevo (JSON, sanitizado).</summary>
        public string? PayloadAfter { get; set; }

        /// <summary>Hash SHA-256 (hex) de la entrada cronologicamente previa. "GENESIS" para la primera.</summary>
        public string PrevHash { get; set; } = "GENESIS";

        /// <summary>Hash SHA-256 (hex) de la forma canonica de ESTA entrada (incluye PrevHash).</summary>
        public string EntryHash { get; set; } = string.Empty;

        /// <summary>Representacion canonica que se firma. Deterministica y ordenada.</summary>
        public string CanonicalForm()
        {
            // Formato lineal clave=valor con escapes; evita ambiguedades de JSON serializer.
            var sb = new StringBuilder();
            AppendField(sb, "id", Id.ToString("D"));
            AppendField(sb, "at", AtUtc.ToString("O"));
            AppendField(sb, "actor", Actor);
            AppendField(sb, "action", Action);
            AppendField(sb, "target", TargetId ?? "");
            AppendField(sb, "detail", Detail ?? "");
            AppendField(sb, "corr", CorrelationId ?? "");
            AppendField(sb, "ok", Succeeded ? "1" : "0");
            AppendField(sb, "err", ErrorMessage ?? "");
            AppendField(sb, "ip", SourceIp ?? "");
            AppendField(sb, "ua", UserAgent ?? "");
            AppendField(sb, "sev", ((int)Severity).ToString());
            AppendField(sb, "before", PayloadBefore ?? "");
            AppendField(sb, "after", PayloadAfter ?? "");
            AppendField(sb, "prev", PrevHash);
            return sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=');
            // Escapar \n, \r, \ para que la forma sea de una sola linea y no inyectable
            var escaped = value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
            sb.Append(escaped).Append('\n');
        }

        public void ComputeAndSetHash()
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalForm());
            var hash = SHA256.HashData(bytes);
            EntryHash = Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>Verifica que EntryHash coincida con CanonicalForm (no verifica la cadena, solo este nodo).</summary>
        public bool VerifySelf()
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalForm());
            var hash = SHA256.HashData(bytes);
            var expected = Convert.ToHexString(hash).ToLowerInvariant();
            return string.Equals(expected, EntryHash, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Sanitizador de payloads: quita valores de claves que parezcan sensibles
    /// (password, token, secret, key, authorization, cookie, apikey) recursivamente en JSON.
    /// </summary>
    public static class AuditSanitizer
    {
        private static readonly string[] SensitiveTokens = new[]
        {
            "password", "passwd", "pwd", "secret", "token", "apikey", "api_key",
            "authorization", "auth", "cookie", "session", "private", "credential"
        };

        public static string? Sanitize(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return payload;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var sanitized = SanitizeElement(doc.RootElement);
                return JsonSerializer.Serialize(sanitized);
            }
            catch (JsonException)
            {
                // No es JSON valido: aplicar regex simple
                var result = payload;
                foreach (var tok in SensitiveTokens)
                {
                    result = System.Text.RegularExpressions.Regex.Replace(
                        result,
                        $"({tok}[\"']?\\s*[:=]\\s*[\"']?)([^\"'\\s,}}]+)",
                        "$1***REDACTED***",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                return result;
            }
        }

        private static object? SanitizeElement(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new System.Collections.Generic.Dictionary<string, object?>();
                    foreach (var prop in el.EnumerateObject())
                    {
                        if (IsSensitiveKey(prop.Name))
                            dict[prop.Name] = "***REDACTED***";
                        else
                            dict[prop.Name] = SanitizeElement(prop.Value);
                    }
                    return dict;
                case JsonValueKind.Array:
                    var list = new System.Collections.Generic.List<object?>();
                    foreach (var item in el.EnumerateArray())
                        list.Add(SanitizeElement(item));
                    return list;
                case JsonValueKind.String: return el.GetString();
                case JsonValueKind.Number: return el.GetRawText();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                default: return null;
            }
        }

        private static bool IsSensitiveKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var lower = key.ToLowerInvariant();
            foreach (var tok in SensitiveTokens)
                if (lower.Contains(tok)) return true;
            return false;
        }
    }
}
