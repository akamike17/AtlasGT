using AtlasGT.Domain.Entities;
using AtlasGT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AtlasGT.Infrastructure.Services
{
    public class DeviceConfigurationService : IDeviceConfigurationService
    {
        private readonly ILogger<DeviceConfigurationService> _logger;
        private readonly List<DeviceConfig> _configs = new();

        public DeviceConfigurationService(ILogger<DeviceConfigurationService> logger)
        {
            _logger = logger;
        }

        public async Task<DeviceConfig> GetConfigAsync(Guid id)
        {
            var config = _configs.FirstOrDefault(c => c.Id == id);
            if (config == null) _logger.LogError($"Device config {id} not found");
            return await Task.FromResult(config);
        }

        public async Task UpdateConfigAsync(DeviceConfig config)
        {
            var existing = _configs.FirstOrDefault(c => c.Id == config.Id);
            if (existing != null)
            {
                _configs.Remove(existing);
            }
            _configs.Add(config);
            _logger.LogInformation($"Updated config for device {config.Name}");
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<DeviceConfig>> GetAllConfigsAsync()
        {
            return await Task.FromResult(_configs.AsEnumerable());
        }
    }
}
