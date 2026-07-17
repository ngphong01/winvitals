

namespace WinVitals.Core.Rules;

/// <summary>
/// Ánh xạ path + rule match → ItemCategory hiển thị cho user.
/// Rule ID là nguồn tin cậy nhất, fallback theo path/extension.
/// </summary>
public static class CategoryClassifier
{
    public static ItemCategory Classify(string ruleId, string path, string ext)
    {
        // Ánh xạ theo rule ID trước – deterministic
        var byRule = ruleId switch
        {
            "temp_files" => ItemCategory.TempFile,
            "prefetch" => ItemCategory.Prefetch,
            "thumbnail_cache" => ItemCategory.ThumbnailCache,
            "crash_dumps" => ItemCategory.CrashDump,
            "browser_cache" => ItemCategory.BrowserCache,
            "old_logs" => ItemCategory.LogFile,
            "windows_update_cache" => ItemCategory.WindowsUpdateCache,
            "old_installers" => ItemCategory.OldInstaller,
            "large_downloads" => ItemCategory.LargeFile,
            var s when s.StartsWith("dev_") => ItemCategory.DevCache,
            _ => (ItemCategory?)null
        };
        if (byRule is not null) return byRule.Value;

        // Fallback theo extension
        return ext.ToLowerInvariant() switch
        {
            ".tmp" or ".temp" => ItemCategory.TempFile,
            ".log" => ItemCategory.LogFile,
            ".dmp" or ".mdmp" or ".hdmp" => ItemCategory.CrashDump,
            ".iso" or ".msi" => ItemCategory.OldInstaller,
            _ => ItemCategory.Unknown
        };
    }
}
