namespace App.Storage.Services;

/// <summary>
/// Interface for file system operations including scanning, moving, and deleting files
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Scans a file or directory and extracts metadata
    /// </summary>
    /// <param name="path">Path to file or directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File metadata including size, dates, and attributes</returns>
    Task<FileMetadata?> ScanFileAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans a directory recursively and extracts metadata for all files
    /// </summary>
    /// <param name="directoryPath">Directory path to scan</param>
    /// <param name="maxDepth">Maximum recursion depth (-1 for unlimited)</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of file metadata</returns>
    Task<List<FileMetadata>> ScanDirectoryAsync(
        string directoryPath,
        int maxDepth = -1,
        IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a file to quarantine location while preserving original directory structure
    /// </summary>
    /// <param name="sourcePath">Original file path</param>
    /// <param name="quarantineBasePath">Base quarantine directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New quarantine path</returns>
    Task<string> MoveToQuarantineAsync(
        string sourcePath,
        string quarantineBasePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a file from quarantine to its original location
    /// </summary>
    /// <param name="quarantinePath">Current quarantine path</param>
    /// <param name="originalPath">Original file path to restore to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if restore succeeded</returns>
    Task<bool> RestoreFromQuarantineAsync(
        string quarantinePath,
        string originalPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely deletes a file with optional backup to quarantine
    /// </summary>
    /// <param name="filePath">Path to file to delete</param>
    /// <param name="skipQuarantine">If true, permanently deletes without quarantine</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deletion succeeded</returns>
    Task<bool> SafeDeleteAsync(
        string filePath,
        bool skipQuarantine = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file or directory exists
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if exists</returns>
    bool Exists(string path);

    /// <summary>
    /// Gets the size of a file in bytes
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <returns>File size in bytes</returns>
    long GetFileSize(string filePath);

    /// <summary>
    /// Gets the size of a directory and all its contents
    /// </summary>
    /// <param name="directoryPath">Directory path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total size in bytes</returns>
    Task<long> GetDirectorySizeAsync(string directoryPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// File metadata extracted during scanning
/// </summary>
public class FileMetadata
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime LastAccessed { get; set; }
    public FileAttributes Attributes { get; set; }
    public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
    public bool IsHidden => (Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
    public bool IsSystemFile => (Attributes & FileAttributes.System) == FileAttributes.System;
    public string? Hash { get; set; }
}
