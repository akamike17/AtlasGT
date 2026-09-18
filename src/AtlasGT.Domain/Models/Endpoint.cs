using System;

namespace AtlasGT.Domain.Models
{
    public class Endpoint
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>FK a <see cref="Interface"/>.</summary>
        public Guid? InterfaceId { get; set; }

        /// <summary>Direccion canonica ("tcp://127.0.0.1:5000", "serial://COM3?baud=9600").</summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>Escalera de confianza (spec sec. 2).</summary>
        public TrustTier TrustTier { get; set; } = TrustTier.Passive;

        /// <summary>Ultima vez que se observo trafico.</summary>
        public DateTime? LastObservedAtUtc { get; set; }
    }
}
