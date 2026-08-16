using System.Text.Json;
using App.Core;

namespace App.Cleaner;

/// <summary>
/// Risk Engine - assesses deletion risk based on path, category, and protected paths
/// </summary>
public class RiskEngine : IRiskEngine
{
    private readonly List<ProtectedPath> _protectedPaths = [];
    private readonly string _rulesDir;

    public RiskEngine(string rulesDir)
    {
        _rulesDir = rulesDir;
        LoadProtectedPaths();
    }

    private void LoadProtectedPaths()
    {
        var file = Path.Combine(_rulesDir, "protected-paths.json");
        if (!File.Exists(file)) return;

        try
        {
            var json = File.ReadAllText(file);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var paths = JsonSerializer.Deserialize<List<ProtectedPath>>(json, options);
            if (paths != null) _protectedPaths.AddRange(paths);
        }
        catch { /* ignore */ }
    }

    public RiskLevel AssessRisk(string path, ItemCategory category,
        long sizeBytes = 0, DateTime? lastModified = null)
    {
        // Check protected paths first
        if (IsProtected(path)) return RiskLevel.Critical;

        // Category-based risk assessment
        return category switch
        {
            ItemCategory.TempFile => RiskLevel.Safe,
            ItemCategory.RecycleBin => RiskLevel.Safe,
            ItemCategory.LogFile => RiskLevel.Safe,
            ItemCategory.CrashDump => RiskLevel.Safe,
            ItemCategory.Prefetch => RiskLevel.Low,
            ItemCategory.ThumbnailCache => RiskLevel.Low,
            ItemCategory.WindowsUpdateCache => RiskLevel.Low,
            ItemCategory.DevCache => RiskLevel.Low,
            ItemCategory.BrowserCache => RiskLevel.Medium,
            ItemCategory.OldInstaller => RiskLevel.Medium,
            ItemCategory.LargeFile => RiskLevel.Medium,
            ItemCategory.DuplicateFile => RiskLevel.Medium,
            ItemCategory.OrphanFile => RiskLevel.High,
            ItemCategory.Unknown => RiskLevel.High,
            _ => RiskLevel.Medium
        };
    }

    private static readonly string[] ProtectedFolders =
    [
        "\\windows\\system32\\", "\\windows\\syswow64\\", "\\windows\\winsxs\\",
        "\\windows\\drivers\\", "\\windows\\installer\\", "\\desktop\\", "\\documents\\",
        "\\pictures\\", "\\videos\\", "\\music\\", "\\onedrive\\"
    ];

    private static readonly string[] DevCacheFolderTokens =
    [
        "\\node_modules\\", "\\build\\", "\\dist\\", "\\.next\\", "\\__pycache__\\",
        "\\bin\\debug\\", "\\bin\\release\\", "\\obj\\debug\\", "\\obj\\release\\",
        "\\target\\", "\\.gradle\\", "\\.cache\\", "\\temp\\", "\\tmp\\",
        "\\prefetch\\", "\\$recycle.bin\\", "\\crashdumps\\", "\\logs\\"
    ];

    private static readonly string[] ProtectedExtensions =
    [
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf",
        ".png", ".jpg", ".jpeg", ".mp4", ".zip", ".rar", ".7z", ".tar", ".gz",
        ".sql", ".db", ".sqlite", ".env", ".pem", ".key", ".pfx"
    ];

    public bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        var norm = path.Replace('/', '\\').ToLowerInvariant();

        // If path is inside a recognized temp/cache folder, it is NOT protected
        if (DevCacheFolderTokens.Any(c => norm.Contains(c))) return false;

        // 1. Built-in protected folders check
        if (ProtectedFolders.Any(f => norm.Contains(f))) return true;

        // 2. Protected extensions check for user files
        var ext = Path.GetExtension(norm);
        if (!string.IsNullOrEmpty(ext) && ProtectedExtensions.Contains(ext)) return true;

        // 3. User configured JSON protected paths
        return _protectedPaths.Any(p => MatchesProtected(path, p.Path));
    }

    public List<string> GetProtectedPaths() => _protectedPaths.Select(p => p.Path).ToList();

    private static bool MatchesProtected(string path, string protectedPath)
    {
        var normalizedPath = path.Replace('/', '\\').ToLowerInvariant();
        var normalizedProtected = protectedPath.Replace('/', '\\').ToLowerInvariant();

        if (normalizedProtected.Contains('*'))
        {
            // Simple wildcard matching
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(normalizedProtected)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(normalizedPath, regex);
        }

        return normalizedPath.Contains(normalizedProtected);
    }

    private class ProtectedPath
    {
        public string Path { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Risk { get; set; } = "High";
    }
}
