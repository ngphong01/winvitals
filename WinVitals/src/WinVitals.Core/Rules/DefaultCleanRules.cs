using WinVitals.Core.Entities;

namespace WinVitals.Core.Rules;

/// <summary>
/// Rules mặc định cho các preset. Load được từ JSON để user tùy chỉnh,
/// nếu không có JSON thì dùng các rule ở đây.
/// </summary>
public static class DefaultCleanRules
{
    public static IReadOnlyList<CleanRule> All { get; } = new List<CleanRule>
    {
        // ─── Quick ─────────────────────────────────────────────
        new()
        {
            Id = "temp_files", Priority = 80, Preset = ScanPreset.Quick,
            Name = "Temporary files",
            PathPatterns = { @"**\Temp\**", @"**\Temporary Internet Files\**",
                             @"**\AppData\Local\Temp\**" },
            Extensions = { ".tmp", ".temp" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "prefetch", Priority = 80, Preset = ScanPreset.Quick,
            Name = "Prefetch",
            PathPatterns = { @"**\Windows\Prefetch\**" },
            Extensions = { ".pf" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "thumbnail_cache", Priority = 191, Preset = ScanPreset.Quick,
            Name = "Thumbnail cache",
            PathPatterns = { @"**\Microsoft\Windows\Explorer\**" },
            Extensions = { ".db" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "crash_dumps", Priority = 78, Preset = ScanPreset.Quick,
            Name = "Crash dumps",
            Extensions = { ".dmp", ".mdmp", ".hdmp" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "browser_cache", Priority = 78, Preset = ScanPreset.Quick,
            Name = "Browser cache",
            PathPatterns = {
                @"**\Google\Chrome\User Data\*\Cache\**",
                @"**\Microsoft\Edge\User Data\*\Cache\**",
                @"**\Mozilla\Firefox\Profiles\*\cache2\**"
            },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "old_logs", Priority = 70, Preset = ScanPreset.Quick,
            Name = "Old log files",
            Extensions = { ".log" }, MaxAgeDays = 30, MinSizeBytes = 1024,
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Low
        },

        // ─── Deep ──────────────────────────────────────────────
        new()
        {
            Id = "windows_update_cache", Priority = 75, Preset = ScanPreset.Deep,
            Name = "Windows Update cache",
            PathPatterns = { @"**\Windows\SoftwareDistribution\Download\**" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Low
        },
        new()
        {
            Id = "old_installers", Priority = 70, Preset = ScanPreset.Deep,
            Name = "Old installers",
            Extensions = { ".iso", ".msi", ".exe" },
            MinSizeBytes = 100L * 1024 * 1024, MaxAgeDays = 90,
            Action = ItemAction.WarnDelete, Risk = RiskLevel.Medium
        },
        new()
        {
            Id = "large_downloads", Priority = 65, Preset = ScanPreset.Deep,
            Name = "Large old files in Downloads",
            PathPatterns = { @"**\Users\*\Downloads\**" },
            MinSizeBytes = 500L * 1024 * 1024, MaxAgeDays = 60,
            Action = ItemAction.WarnDelete, Risk = RiskLevel.Medium
        },

        // ─── Developer ─────────────────────────────────────────
        new()
        {
            Id = "dev_node_modules", Priority = 85, Preset = ScanPreset.Developer,
            Name = "node_modules",
            PathPatterns = { @"**\node_modules\**" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "dev_build_output", Priority = 85, Preset = ScanPreset.Developer,
            Name = "Build output",
            PathPatterns = {
                @"**\bin\Debug\**", @"**\bin\Release\**", @"**\obj\**",
                @"**\dist\**", @"**\build\**", @"**\out\**",
                @"**\.next\**", @"**\.nuxt\**", @"**\.turbo\**"
            },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "dev_python_cache", Priority = 85, Preset = ScanPreset.Developer,
            Name = "Python cache",
            PathPatterns = { @"**\__pycache__\**", @"**\.pytest_cache\**", @"**\.mypy_cache\**" },
            Extensions = { ".pyc", ".pyo" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        },
        new()
        {
            Id = "dev_gradle_maven", Priority = 85, Preset = ScanPreset.Developer,
            Name = "Gradle/Maven cache",
            PathPatterns = { @"**\.gradle\caches\**", @"**\target\**" },
            Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe
        }
    };
}
