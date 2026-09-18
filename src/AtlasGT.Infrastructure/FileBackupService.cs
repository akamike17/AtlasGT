using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Infrastructure
{
    /// <summary>
    /// Backup y Restore simples del directorio de datos del sistema.
    /// Crea snapshots zip con timestamp en nombre.
    /// </summary>
    public interface IBackupService
    {
        Task<string> CreateBackupAsync(string? destinationDir = null, CancellationToken ct = default);
        Task<bool> RestoreAsync(string zipPath, string? destinationDir = null, CancellationToken ct = default);
        Task<IReadOnlyList<string>> ListBackupsAsync(string? destinationDir = null, CancellationToken ct = default);
    }

    public sealed class FileBackupService : IBackupService
    {
        private readonly string _sourceDir;

        public string SourceDirForDebug => _sourceDir;

        public FileBackupService(string sourceDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir)) throw new ArgumentException("sourceDir requerido", nameof(sourceDir));
            _sourceDir = sourceDir;
        }

        public async Task<string> CreateBackupAsync(string? destinationDir = null, CancellationToken ct = default)
        {
            // Default: subdirectorio hermano pero oculto del source, con nombre unico por source
            // para que multiples instancias en TestInitialize paralelo no compartan el dir de backups.
            var parent = Directory.GetParent(_sourceDir)?.FullName ?? _sourceDir;
            var tag = Path.GetFileName(_sourceDir.TrimEnd(Path.DirectorySeparatorChar));
            var dest = destinationDir ?? Path.Combine(parent, $".backups-{tag}");
            Directory.CreateDirectory(dest);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            // Sufijo para evitar colision cuando dos backups corren dentro del mismo segundo.
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var fileName = $"atlasgt-backup-{stamp}-{suffix}.zip";
            var fullPath = Path.Combine(dest, fileName);

            if (!Directory.Exists(_sourceDir))
                throw new InvalidOperationException($"sourceDir no existe: {_sourceDir}");

            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(_sourceDir, fullPath);
            }, ct).ConfigureAwait(false);

            if (!File.Exists(fullPath))
                throw new InvalidOperationException($"zip no aparecio despues de CreateFromDirectory: {fullPath}");

            return fullPath;
        }

        public async Task<bool> RestoreAsync(string zipPath, string? destinationDir = null, CancellationToken ct = default)
        {
            if (!File.Exists(zipPath)) return false;
            var dest = destinationDir ?? _sourceDir;
            Directory.CreateDirectory(dest);
            try
            {
                await Task.Run(() =>
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dest, overwriteFiles: true);
                }, ct).ConfigureAwait(false);
                return true;
            }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
        }

        public Task<IReadOnlyList<string>> ListBackupsAsync(string? destinationDir = null, CancellationToken ct = default)
        {
            var parent = Directory.GetParent(_sourceDir)?.FullName ?? _sourceDir;
            var tag = Path.GetFileName(_sourceDir.TrimEnd(Path.DirectorySeparatorChar));
            var dest = destinationDir ?? Path.Combine(parent, $".backups-{tag}");
            if (!Directory.Exists(dest))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            var files = Directory.GetFiles(dest, "atlasgt-backup-*.zip");
            Array.Sort(files);
            return Task.FromResult<IReadOnlyList<string>>(files);
        }
    }
}
