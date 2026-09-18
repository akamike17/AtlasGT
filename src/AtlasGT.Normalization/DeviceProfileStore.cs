using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Normalization
{
    /// <summary>
    /// Persistencia y versionado de DeviceProfile en JSON.
    /// </summary>
    public interface IDeviceProfileStore
    {
        Task SaveAsync(DeviceProfile profile, CancellationToken ct = default);
        Task<DeviceProfile?> LoadAsync(Guid id, CancellationToken ct = default);
        Task<DeviceProfile?> FindAsync(string manufacturer, string model, string? firmware, CancellationToken ct = default);
    }

    public sealed class JsonDeviceProfileStore : IDeviceProfileStore
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        private readonly string _dir;

        public JsonDeviceProfileStore(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) throw new ArgumentException("dir requerido", nameof(dir));
            _dir = dir;
            Directory.CreateDirectory(_dir);
        }

        public async Task SaveAsync(DeviceProfile profile, CancellationToken ct = default)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            var path = Path.Combine(_dir, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, Json);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }

        public async Task<DeviceProfile?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            var path = Path.Combine(_dir, $"{id}.json");
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<DeviceProfile>(json, Json);
        }

        public async Task<DeviceProfile?> FindAsync(string manufacturer, string model, string? firmware, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(manufacturer)) return null;
            if (string.IsNullOrWhiteSpace(model)) return null;

            foreach (var file in Directory.GetFiles(_dir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var p = JsonSerializer.Deserialize<DeviceProfile>(json, Json);
                    if (p is null) continue;
                    if (!string.Equals(p.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(p.Model, model, StringComparison.OrdinalIgnoreCase)) continue;
                    if (firmware is not null && !string.Equals(p.FirmwareVersion, firmware, StringComparison.OrdinalIgnoreCase)) continue;
                    return p;
                }
                catch (JsonException) { /* skip */ }
                catch (IOException) { /* skip */ }
            }
            return null;
        }
    }
}
