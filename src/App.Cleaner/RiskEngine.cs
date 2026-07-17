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

    public bool IsProtected(string path)
    {
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
