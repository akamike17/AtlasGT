using System;
using System.Collections.Concurrent;
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
    /// Buffer store-and-forward para observaciones (sec. 16).
    /// Todo item persiste localmente ANTES de intentar forward. Si el forward
    /// falla, queda pendiente para reintento. Deduplicacion por Observation.Id.
    /// </summary>
    public interface IStoreAndForwardBuffer
    {
        Task EnqueueAsync(Observation obs, CancellationToken ct = default);
        Task<int> PendingCountAsync(CancellationToken ct = default);

        /// <summary>Intenta drenar hasta <paramref name="max"/> items al destino dado. Devuelve entregados.</summary>
        Task<int> DrainAsync(Func<Observation, CancellationToken, Task<bool>> deliver, int max, CancellationToken ct = default);
    }

    public sealed class FileStoreAndForwardBuffer : IStoreAndForwardBuffer
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        private readonly string _dir;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly HashSet<Guid> _seen = new();

        public FileStoreAndForwardBuffer(string dir)
        {
            _dir = dir ?? throw new ArgumentNullException(nameof(dir));
            Directory.CreateDirectory(Path.Combine(_dir, "pending"));
            Directory.CreateDirectory(Path.Combine(_dir, "sent"));
        }

        private string PendingPath(Guid id) => Path.Combine(_dir, "pending", $"{id}.json");
        private string SentPath(Guid id) => Path.Combine(_dir, "sent", $"{id}.json");

        public async Task EnqueueAsync(Observation obs, CancellationToken ct = default)
        {
            if (obs is null) throw new ArgumentNullException(nameof(obs));
            if (obs.Id == Guid.Empty) obs.Id = Guid.NewGuid();

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Idempotencia: si ya lo vimos (pendiente o enviado) no duplicar
                if (_seen.Contains(obs.Id)) return;
                if (File.Exists(PendingPath(obs.Id)) || File.Exists(SentPath(obs.Id)))
                {
                    _seen.Add(obs.Id);
                    return;
                }
                var json = JsonSerializer.Serialize(obs, Json);
                await File.WriteAllTextAsync(PendingPath(obs.Id), json, ct).ConfigureAwait(false);
                _seen.Add(obs.Id);
            }
            finally { _gate.Release(); }
        }

        public Task<int> PendingCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Directory.GetFiles(Path.Combine(_dir, "pending"), "*.json").Length);
        }

        public async Task<int> DrainAsync(Func<Observation, CancellationToken, Task<bool>> deliver, int max, CancellationToken ct = default)
        {
            if (deliver is null) throw new ArgumentNullException(nameof(deliver));
            var pendingDir = Path.Combine(_dir, "pending");
            var files = Directory.GetFiles(pendingDir, "*.json");
            Array.Sort(files); // orden estable por nombre (= por Id)

            var delivered = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                if (delivered >= max) break;

                Observation? obs = null;
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    obs = JsonSerializer.Deserialize<Observation>(json, Json);
                }
                catch (JsonException) { /* archivo corrupto: mover a sent para no bloquear */ }
                catch (IOException) { continue; }

                if (obs is null)
                {
                    TryMove(file, SentPath(Guid.NewGuid()));
                    continue;
                }

                bool ok;
                try { ok = await deliver(obs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { ok = false; } // destino caido: reintentar luego, NO marcar sent

                if (ok)
                {
                    TryMove(file, SentPath(obs.Id));
                    delivered++;
                }
            }
            return delivered;
        }

        private static void TryMove(string from, string to)
        {
            try { File.Move(from, to, overwrite: true); }
            catch (IOException) { /* lock temporal */ }
        }
    }
}
