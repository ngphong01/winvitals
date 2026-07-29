using App.Core;

namespace App.Storage.Services;

/// <summary>
/// File path utilities for detecting cloud service, browser, and system cache paths.
/// Centralizes all path resolution logic for scanners.
/// Requirements: 7.1, 7.2, 7.3, 8.4, 9.1
/// </summary>
public static class PathUtils
{
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    // === Cloud Service Paths (7.1, 7.2, 7.3) ===

    public static string? OneDrivePath =>
        Path.Combine(UserProfile, "OneDrive");

    public static string[] OneDriveCachePaths => new[]
    {
        Path.Combine(LocalAppData, "Microsoft", "OneDrive", "logs"),
        Path.Combine(LocalAppData, "Microsoft", "OneDrive", "cache"),
    };

    public static string[] GoogleDriveCachePaths => new[]
    {
        Path.Combine(LocalAppData, "Google", "DriveFS"),
        Path.Combine(ProgramData, "Google", "DriveFS"),
    };

    public static string[] DropboxCachePaths => new[]
    {
        Path.Combine(LocalAppData, "Dropbox", "instance1"),
        Path.Combine(AppData, "Dropbox", ".dropbox.cache"),
    };

    // === Browser Cache Paths (7.4, 7.8) ===

    public static string? ChromeCachePath =>
        Path.Combine(LocalAppData, "Google", "Chrome", "User Data", "Default", "Cache");

    public static string? ChromeCodeCachePath =>
        Path.Combine(LocalAppData, "Google", "Chrome", "User Data", "Default", "Code Cache");

    public static string? FirefoxCachePath =>
        Path.Combine(LocalAppData, "Mozilla", "Firefox", "Profiles");

    public static string? EdgeCachePath =>
        Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data", "Default", "Cache");

    public static string[] AllBrowserCachePaths => new[]
    {
        ChromeCachePath, ChromeCodeCachePath, FirefoxCachePath, EdgeCachePath
    }.Where(p => p != null && Directory.Exists(p)).Cast<string>().ToArray();

    // === Windows Store Cache (9.1, 9.2, 9.3) ===

    public static string? WindowsStoreCachePath =>
        Path.Combine(LocalAppData, "Packages");

    public static string? WindowsUpdateCachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");

    public static string? WindowsTempPath => Path.GetTempPath();

    // === Developer Paths ===

    public static string[] CommonDevDirs => new[]
    {
        Path.Combine(UserProfile, "source"),
        Path.Combine(UserProfile, "projects"),
        Path.Combine(UserProfile, "dev"),
        Path.Combine(UserProfile, "Documents", "GitHub"),
        Path.Combine(UserProfile, "source", "repos"),
    };

    // === Preset Framework Paths (10.1-10.10) ===

    public static readonly Dictionary<string, string[]> FrameworkPresets = new()
    {
        ["React"] = new[] { "node_modules", "build", ".next" },
        ["Angular"] = new[] { "node_modules", "dist", ".angular" },
        ["Vue"] = new[] { "node_modules", "dist", ".nuxt" },
        [".NET"] = new[] { "bin", "obj", "packages" },
        ["Python"] = new[] { "__pycache__", ".venv", "venv", ".tox", "dist" },
        ["Node.js"] = new[] { "node_modules", ".npm", ".cache" },
        ["Flutter/Dart"] = new[] { "build", ".dart_tool", ".packages" },
        ["Go"] = new[] { "vendor" },
        ["Java/Gradle"] = new[] { "build", ".gradle", "target" },
        ["Rust"] = new[] { "target" },
    };

    // === File size helpers ===

    /// <summary>
    /// Gets total size of a directory (non-recursive child sizes provided separately).
    /// Full recursive size calculation. Use sparingly — prefer cached results.
    /// </summary>
    public static long GetDirectorySizeSafe(string path)
    {
        try
        {
            long size = 0;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    /// <summary>
    /// Returns true if the path is within a cloud-synced directory.
    /// </summary>
    public static bool IsCloudPath(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if (OneDrivePath != null && normalized.StartsWith(OneDrivePath, StringComparison.OrdinalIgnoreCase))
            return true;
        if (normalized.Contains("GoogleDrive", StringComparison.OrdinalIgnoreCase))
            return true;
        if (normalized.Contains("Dropbox", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
