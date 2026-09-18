using System;
using System.Collections.Generic;

namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Evidencia concreta observada: timestamp, valores, contexto.
    /// Apoya hipotesis y profiles.
    /// </summary>
    public class Evidence
    {
        public Guid Id { get; set; }
        public Guid? AssetId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset AtUtc { get; set; }
        public string? Source { get; set; } // "operator", "operator-simulator", "modbus-client"
        public List<SignalValueSnapshot> Signals { get; set; } = new();
        public string? Notes { get; set; }
    }

    public class SignalValueSnapshot
    {
        public string SignalKey { get; set; } = string.Empty;
        public double? Value { get; set; }
        public string? ValueText { get; set; }
        public QualityKind Quality { get; set; }
        public DateTimeOffset AtUtc { get; set; }
    }
}
