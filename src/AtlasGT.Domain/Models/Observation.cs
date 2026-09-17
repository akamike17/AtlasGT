using System;

namespace AtlasGT.Domain.Models
{
    public class Observation
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// The measured value (if applicable).
        /// </summary>
        public double? Value { get; set; }
        /// <summary>
        /// The unit of the value (e.g., "°C", "V", "A").
        /// </summary>
        public string Unit { get; set; } = string.Empty;
    }
}
