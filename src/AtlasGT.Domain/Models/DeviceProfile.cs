using System;

namespace AtlasGT.Domain.Models
{
    /// <summary>
    /// Perfil reutilizable para un tipo de dispositivo.
    /// Contiene identidades, protocolos, mapas, pruebas.
    /// </summary>
    public class DeviceProfile
    {
        public Guid Id { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Controller { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? Notes { get; set; }
        public string? Protocol { get; set; } // "Modbus RTU", "HTTP", etc.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public VersionInfo? Version { get; set; }
        public bool IsVerified { get; set; }
    }

    public class VersionInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string? Hash { get; set; }
    }
}
