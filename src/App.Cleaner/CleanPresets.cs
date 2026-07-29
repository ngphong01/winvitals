namespace App.Cleaner;

/// <summary>
/// Clean Presets — one-click cleaning profiles for common scenarios.
/// Each preset targets a specific use case with pre-configured scanning + cleaning.
/// Requirements: v2.4 Smart Clean Presets
/// </summary>
public static class CleanPresets
{
    /// <summary>
    /// All available presets with metadata.
    /// </summary>
    public static List<CleanPreset> All =>
    [
        QuickSystemPreset,
        DeveloperPreset,
        DeepMonthlyPreset,
        DriveCCleaner,
        PrivacyPreset,
        GamingPreset,
        DesignPreset
    ];

    /// <summary>
    /// Quick System — temp files, recycle bin, logs, crash dumps, browser cache.
    /// Safe for daily use. Takes ~30 seconds.
    /// </summary>
    public static CleanPreset QuickSystemPreset { get; } = new()
    {
        Id = "quick_system",
        Name = "Dọn Nhanh Hệ Thống",
        Icon = "⚡",
        Description = "Temp files, Recycle Bin, crash dumps, browser cache, prefetch. An toàn tuyệt đối.",
        EstimatedCleanable = "1-5 GB",
        Duration = "~30 giây",
        CleanLevel = Core.CleanLevel.Quick,
        Categories =
        [
            Core.ItemCategory.TempFile, Core.ItemCategory.LogFile,
            Core.ItemCategory.CrashDump, Core.ItemCategory.RecycleBin,
            Core.ItemCategory.BrowserCache, Core.ItemCategory.Prefetch,
            Core.ItemCategory.ThumbnailCache
        ],
        SafeToAuto = true,
    };

    /// <summary>
    /// Developer — package caches, build artifacts, Docker, stale node_modules.
    /// For developers. Can reclaim 10-50GB+.
    /// </summary>
    public static CleanPreset DeveloperPreset { get; } = new()
    {
        Id = "developer",
        Name = "Dọn Cho Developer",
        Icon = "☕",
        Description = "npm/Pip/NuGet/Cargo caches, node_modules stale, Docker images, build artifacts.",
        EstimatedCleanable = "10-50+ GB",
        Duration = "~2-5 phút",
        CleanLevel = Core.CleanLevel.Developer,
        Categories =
        [
            Core.ItemCategory.DevCache
        ],
        SafeToAuto = false,
        Note = "Cần review trước — một số cache giúp build nhanh hơn."
    };

    /// <summary>
    /// Deep Monthly — Windows Update cache, old installers, app leftovers, large old files.
    /// Recommended once per month.
    /// </summary>
    public static CleanPreset DeepMonthlyPreset { get; } = new()
    {
        Id = "deep_monthly",
        Name = "Dọn Sâu Hàng Tháng",
        Icon = "📅",
        Description = "Windows Update cache, installer cũ, file >1GB không dùng 90+ ngày, app leftovers.",
        EstimatedCleanable = "5-30 GB",
        Duration = "~5-10 phút",
        CleanLevel = Core.CleanLevel.Deep,
        Categories = null, // All categories
        SafeToAuto = false,
        Note = "Nên chạy preview trước. Có thể xóa file cần thiết nếu không kiểm tra."
    };

    /// <summary>
    /// Privacy — browser cookies, trackers, recent files, temp internet files.
    /// </summary>
    public static CleanPreset PrivacyPreset { get; } = new()
    {
        Id = "privacy",
        Name = "Dọn Riêng Tư",
        Icon = "🛡️",
        Description = "Browser cookies tracking, history, recent files, DNS cache, temp internet.",
        EstimatedCleanable = "0.1-2 GB",
        Duration = "~15 giây",
        CleanLevel = Core.CleanLevel.Quick,
        Categories =
        [
            Core.ItemCategory.BrowserCache, Core.ItemCategory.TempFile
        ],
        SafeToAuto = true,
        Note = "Một số website sẽ yêu cầu đăng nhập lại sau khi dọn."
    };

    /// <summary>
    /// Gaming — NVIDIA/AMD shader cache, DirectX cache, Steam workshop temp.
    /// </summary>
    public static CleanPreset GamingPreset { get; } = new()
    {
        Id = "gaming",
        Name = "Dọn Cho Gamer",
        Icon = "🎮",
        Description = "Shader cache (NVIDIA/AMD), DirectX shader cache, Steam download cache.",
        EstimatedCleanable = "2-15 GB",
        Duration = "~1-2 phút",
        CleanLevel = Core.CleanLevel.Deep,
        Categories = null,
        SafeToAuto = true,
        Note = "Game sẽ load chậm hơn lần đầu sau khi dọn shader cache."
    };

    /// <summary>
    /// Design — Adobe cache, Figma cache, Blender temp, render outputs.
    /// </summary>
    public static CleanPreset DesignPreset { get; } = new()
    {
        Id = "design",
        Name = "Dọn Cho Designer",
        Icon = "🎨",
        Description = "Adobe Media Cache, Figma cache, Blender tmp, render scratch files.",
        EstimatedCleanable = "5-50+ GB",
        Duration = "~2-5 phút",
        CleanLevel = Core.CleanLevel.Deep,
        Categories = null,
        SafeToAuto = false,
        Note = "Adobe Media Cache giúp preview nhanh — chỉ dọn project cũ."
    };
    public static CleanPreset DriveCCleaner { get; } = new()
    {
        Id = "drive_c",
        Name = "Dọn Ổ C:",
        Icon = "💾",
        Description = "Temp, Recycle Bin, Win Update cache, downloads cũ, file >1GB >90 ngày, browser cache.",
        EstimatedCleanable = "10-50+ GB",
        Duration = "~3-5 phút",
        CleanLevel = Core.CleanLevel.Deep,
        Categories = null,
        SafeToAuto = false,
        Note = "Chạy Preview trước. Không ảnh hưởng Windows updates."
    };
}

/// <summary>
/// A pre-configured cleaning profile.
/// </summary>
public class CleanPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Description { get; init; } = "";
    public string EstimatedCleanable { get; init; } = "";
    public string Duration { get; init; } = "";
    public Core.CleanLevel CleanLevel { get; init; }
    public List<Core.ItemCategory>? Categories { get; init; }
    public bool SafeToAuto { get; init; }
    public string? Note { get; init; }
}
