using System;

namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Senal normalizada. Puede ser medicion, contador, estado, comando.
    /// </summary>
    public class Signal
    {
        public Guid Id { get; set; }
        public Guid? AssetId { get; set; }
        public string Key { get; set; } = string.Empty; // "motor.current", "prensa.count", etc.
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public double? Value { get; set; }
        public string? ValueText { get; set; }
        public QualityKind Quality { get; set; } = QualityKind.Unknown;
        public ProvenanceKind Provenance { get; set; } = ProvenanceKind.Unknown;
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? SourceTimestamp { get; set; }
    }
}
