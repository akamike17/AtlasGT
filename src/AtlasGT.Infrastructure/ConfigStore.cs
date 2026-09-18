using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Infrastructure
{
    /// <summary>
    /// ConfigStore: persistencia file-based JSON de la configuracion viva del gateway:
    /// Assets, Devices, Endpoints, DeviceProfiles, reglas de alarma y reglas derivadas.
    /// Offline-first (sin DB externa), append-versionado por archivo con lock en proceso.
    /// Ruta default: {cwd}/data/config/config.json
    /// </summary>
    public sealed class ConfigStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly string _path;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ConfigStore(string? path = null)
        {
            _path = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Directory.GetCurrentDirectory(), "data", "config", "config.json")
                : path;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }

        public string PathOnDisk => _path;

        public async Task<ConfigSnapshot> LoadAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!File.Exists(_path)) return new ConfigSnapshot();
                var json = await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false);
                var snap = JsonSerializer.Deserialize<ConfigSnapshot>(json, JsonOpts);
                return snap ?? new ConfigSnapshot();
            }
            catch (JsonException)
            {
                // Config corrupta no tumba el gateway: devolver snapshot vacio.
                return new ConfigSnapshot();
            }
            finally { _gate.Release(); }
        }

        public async Task SaveAsync(ConfigSnapshot snapshot, CancellationToken ct = default)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tmp = _path + ".tmp";
                var json = JsonSerializer.Serialize(snapshot, JsonOpts);
                await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
                File.Move(tmp, _path, overwrite: true);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Aplica una mutacion bajo lock y persiste. Devuelve el resultado de la mutacion.</summary>
        public async Task<T> MutateAsync<T>(Func<ConfigSnapshot, T> mutator, CancellationToken ct = default)
        {
            if (mutator is null) throw new ArgumentNullException(nameof(mutator));
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snap = File.Exists(_path)
                    ? JsonSerializer.Deserialize<ConfigSnapshot>(await File.ReadAllTextAsync(_path, ct).ConfigureAwait(false), JsonOpts) ?? new ConfigSnapshot()
                    : new ConfigSnapshot();
                var result = mutator(snap);
                var tmp = _path + ".tmp";
                var json = JsonSerializer.Serialize(snap, JsonOpts);
                await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
                File.Move(tmp, _path, overwrite: true);
                return result;
            }
            finally { _gate.Release(); }
        }
    }

    /// <summary>Estado completo de configuracion persistido.</summary>
    public sealed class ConfigSnapshot
    {
        public List<Asset> Assets { get; set; } = new();
        public List<Device> Devices { get; set; } = new();
        public List<Endpoint> Endpoints { get; set; } = new();
        public List<DeviceProfile> Profiles { get; set; } = new();
        public List<AlarmRuleDto> AlarmRules { get; set; } = new();
        public List<DerivedRuleDto> DerivedRules { get; set; } = new();
        public int SchemaVersion { get; set; } = 1;
    }

    public sealed class AlarmRuleDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string SignalKey { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public string Comparison { get; set; } = "GreaterThan";
        public string Severity { get; set; } = "Warning";
        public int Debounce { get; set; } = 3;
        public int MaxPerMinute { get; set; } = 10;
    }

    public sealed class DerivedRuleDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string OutputSignalKey { get; set; } = string.Empty;
        public string Input1 { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public string Input2 { get; set; } = string.Empty;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(OutputSignalKey) &&
            !string.IsNullOrWhiteSpace(Input1) &&
            !string.IsNullOrWhiteSpace(Input2);
    }
}
