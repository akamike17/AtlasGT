using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Historian
{
    /// <summary>
    /// Persistencia de <see cref="Observation"/> en archivos JSONL rotados por dia (UTC).
    /// Offline-first: no depende de ningun servicio externo. Append-only,
    /// con lock en proceso. Ruta default: {cwd}/data/observations/YYYYMMDD.jsonl
    /// </summary>
    public interface IObservationHistorian
    {
        Task AppendAsync(Observation observation, CancellationToken cancellationToken = default);

        /// <summary>Devuelve todas las observaciones de un dia UTC dado (formato YYYYMMDD).</summary>
        IAsyncEnumerable<Observation> ReadDayAsync(string dayUtc, CancellationToken cancellationToken = default);

        /// <summary>Retorna la ruta del archivo actual (para tests/debug).</summary>
        string GetCurrentFilePath();
    }

    public sealed class FileObservationHistorian : IObservationHistorian
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly string _baseDir;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public FileObservationHistorian(string? baseDir = null)
        {
            _baseDir = string.IsNullOrWhiteSpace(baseDir)
                ? Path.Combine(Directory.GetCurrentDirectory(), "data", "observations")
                : baseDir;
            Directory.CreateDirectory(_baseDir);
        }

        public string GetCurrentFilePath()
        {
            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            return Path.Combine(_baseDir, $"{day}.jsonl");
        }

        public async Task AppendAsync(Observation observation, CancellationToken cancellationToken = default)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            var path = GetCurrentFilePath();
            var line = JsonSerializer.Serialize(observation, JsonOpts) + "\n";

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(path, line, Encoding.UTF8, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async IAsyncEnumerable<Observation> ReadDayAsync(
            string dayUtc,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dayUtc)) throw new ArgumentException("dayUtc requerido", nameof(dayUtc));
            var path = Path.Combine(_baseDir, $"{dayUtc}.jsonl");
            if (!File.Exists(path)) yield break;

            using var sr = new StreamReader(path, Encoding.UTF8);
            string? line;
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;
                Observation? obs = null;
                try
                {
                    obs = JsonSerializer.Deserialize<Observation>(line, JsonOpts);
                }
                catch (JsonException)
                {
                    // Linea corrupta: saltar (historian no debe morir por una fila mala).
                }
                if (obs is not null) yield return obs;
            }
        }
    }
}
