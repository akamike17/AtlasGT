using System;

namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Una lectura observada desde un endpoint. Corazon del pipeline.
    /// Se crea siempre en estado observado (TrustTier max = Observed)
    /// y puede ser enriquecida despues por normalizacion/decodificacion.
    /// </summary>
    public class Observation
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>FK al Endpoint del que salio esta observacion.</summary>
        public Guid? EndpointId { get; set; }

        /// <summary>FK a la Signal (opcional hasta que haya normalizacion).</summary>
        public Guid? SignalId { get; set; }

        /// <summary>Valor medido, si aplica.</summary>
        public double? Value { get; set; }

        /// <summary>Unidad (ej. "°C", "V", "A").</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Payload crudo original (base64 o hex) — evidencia primaria.</summary>
        public byte[]? RawPayload { get; set; }

        /// <summary>Transporte de origen ("tcp", "udp", "serial").</summary>
        public string Transport { get; set; } = string.Empty;

        /// <summary>Timestamp UTC cuando se recibio en el conector.</summary>
        public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Confianza maxima alcanzada. Observacion pura = Observed.</summary>
        public TrustTier TrustTier { get; set; } = TrustTier.Observed;
    }
}
