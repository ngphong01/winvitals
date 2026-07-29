using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace App.Storage.Services;

/// <summary>
/// File system operations service — scanning, quarantine, safe deletion.
/// Centralizes all file I/O with consistent error handling.
/// Requirements: 1.1, 3.4, 9.9, 13.1
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<FileMetadata?> ScanFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return Task.FromResult<FileMetadata?>(null);

            var info = new FileInfo(path);
            var metadata = new FileMetadata
            {
                Path = path,
                Name = info.Name,
                Extension = info.Extension.ToLowerInvariant(),
                SizeBytes = info is { Exists: true, Length: > 0 } ? info.Length : 0,
                IsDirectory = Directory.Exists(path),
                CreatedAt = info.CreationTime,
                LastModified = info.LastWriteTime,
                LastAccessed = info.LastAccessTime,
                Attributes = info.Attributes
            };

            _logger.LogTrace("Scanned file: {Path} ({Size} bytes)", path, metadata.SizeBytes);
            return Task.FromResult<FileMetadata?>(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan file: {Path}", path);
            return Task.FromResult<FileMetadata?>(null);
        }
    }

    /// <inheritdoc />
    public Task<List<FileMetadata>> ScanDirectoryAsync(
        string directoryPath, int maxDepth = -1,
        IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FileMetadata>();
        if (!Directory.Exists(directoryPath)) return Task.FromResult(results);

        try
        {
            var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true };
            if (maxDepth >= 0) opts.MaxRecursionDepth = maxDepth;

            var files = Directory.GetFiles(directoryPath, "*.*", opts);
            int total = files.Length;
            int done = 0;

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    var info = new FileInfo(file);
                    results.Add(new FileMetadata
                    {
                        Path = file, Name = info.Name, Extension = info.Extension.ToLowerInvariant(),
                        SizeBytes = info is { Exists: true } ? info.Length : 0, IsDirectory = false,
                        CreatedAt = info.CreationTime, LastModified = info.LastWriteTime,
                        LastAccessed = info.LastAccessTime, Attributes = info.Attributes
                    });
                }
                catch { /* skip inaccessible */ }

                done++;
                if (done % 1000 == 0)
                    progress?.Report(($"Scanning: {done}/{total}", done * 100 / total));
            }
            progress?.Report(($"Scanned {results.Count} files", 100));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan directory: {Path}", directoryPath);
        }

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public async Task<string> MoveToQuarantineAsync(
        string sourcePath, string quarantineBasePath,
        CancellationToken cancellationToken = default)
    {
        var relativePath = Path.GetFullPath(sourcePath).Replace(Path.GetPathRoot(sourcePath) ?? "", "").TrimStart(Path.DirectorySeparatorChar);
        var destPath = Path.Combine(quarantineBasePath, relativePath);
        var destDir = Path.GetDirectoryName(destPath)!;

        Directory.CreateDirectory(destDir);

        if (File.Exists(sourcePath))
        {
            await Task.Run(() => File.Move(sourcePath, destPath, overwrite: false), cancellationToken);
        }
        else if (Directory.Exists(sourcePath))
        {
            await Task.Run(() => Directory.Move(sourcePath, destPath), cancellationToken);
        }

        _logger.LogInformation("Quarantined: {Source} → {Dest}", sourcePath, destPath);
        return destPath;
    }

    /// <inheritdoc />
    public Task<bool> RestoreFromQuarantineAsync(
        string quarantinePath, string originalPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(quarantinePath) && !Directory.Exists(quarantinePath))
                return Task.FromResult(false);

            var destDir = Path.GetDirectoryName(originalPath)!;
            Directory.CreateDirectory(destDir);

            if (File.Exists(originalPath))
            {
                // Conflict: rename original
                var backup = originalPath + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(originalPath, backup);
            }

            if (File.Exists(quarantinePath))
                File.Move(quarantinePath, originalPath);
            else if (Directory.Exists(quarantinePath))
                Directory.Move(quarantinePath, originalPath);

            _logger.LogInformation("Restored: {Source} → {Dest}", quarantinePath, originalPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore from quarantine: {Path}", quarantinePath);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> SafeDeleteAsync(
        string filePath, bool skipQuarantine = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(filePath))
            {
                if (skipQuarantine)
                {
                    File.Delete(filePath);
                }
                else
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted: {Path}", filePath);
                }
            }
            else if (Directory.Exists(filePath))
            {
                Directory.Delete(filePath, recursive: true);
                _logger.LogInformation("Deleted directory: {Path}", filePath);
            }
            else
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete: {Path}", filePath);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public long GetFileSize(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch { return 0; }
    }

    /// <inheritdoc />
    public Task<long> GetDirectorySizeAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(directoryPath, "*.*",
                    new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            catch { /* skip */ }
            return size;
        }, cancellationToken);
    }
}
