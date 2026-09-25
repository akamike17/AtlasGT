namespace AtlasGT.Application.Interfaces
{
    public interface IDeviceConfigurationService
    {
        Task<DeviceConfig> GetConfigAsync(Guid id);
        Task UpdateConfigAsync(DeviceConfig config);
        Task<IEnumerable<DeviceConfig>> GetAllConfigsAsync();
    }
}
