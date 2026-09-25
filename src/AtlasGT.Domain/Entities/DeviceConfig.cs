namespace AtlasGT.Domain.Entities
{
    public class DeviceConfig
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
    }
}
