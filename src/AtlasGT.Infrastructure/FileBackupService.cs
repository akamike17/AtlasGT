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

        public FileBackupService(string sourceDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir)) throw new ArgumentException("sourceDir requerido", nameof(sourceDir));
            _sourceDir = sourceDir;
        }

        public async Task<string> CreateBackupAsync(string? destinationDir = null, CancellationToken ct = default)
        {
            // Colocar backups fuera del sourceDir por defecto, para que el zip no se contenga a si mismo.
            var dest = destinationDir ?? Path.Combine(Directory.GetParent(_sourceDir)?.FullName ?? _sourceDir, "backups");
            Directory.CreateDirectory(dest);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var fileName = $"atlasgt-backup-{stamp}.zip";
            var fullPath = Path.Combine(dest, fileName);

            await Task.Run(() =>
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(_sourceDir, fullPath);
            }, ct).ConfigureAwait(false);

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
            var dest = destinationDir ?? Path.Combine(Directory.GetParent(_sourceDir)?.FullName ?? _sourceDir, "backups");
            if (!Directory.Exists(dest))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            var files = Directory.GetFiles(dest, "atlasgt-backup-*.zip");
            Array.Sort(files);
            return Task.FromResult<IReadOnlyList<string>>(files);
        }
    }
}
