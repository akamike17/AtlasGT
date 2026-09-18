using System;
using System.Collections.Generic;

namespace AtlasGT.Domain.Models
{
    public class Device
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>FK a <see cref="Asset"/> (opcional).</summary>
        public Guid? AssetId { get; set; }

        public List<Interface> Interfaces { get; set; } = new();
    }
}
