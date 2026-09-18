using System;
using System.Collections.Generic;

namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Modelo normalizado central. Un Asset es cualquier equipo del que
    /// queremos observar datos (prensa, PLC, sensor, etc.).
    /// </summary>
    public class Asset
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Clave legible para humanos: "PRENSA-04".</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>Jerarquia: Site.Area.Line.Asset.</summary>
        public Guid? AreaId { get; set; }

        public List<Interface> Interfaces { get; set; } = new();
        public List<Signal> Signals { get; set; } = new();
        public List<Evidence> EvidenceLog { get; set; } = new();
    }
}
