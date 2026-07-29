using System.Collections.Concurrent;
using App.Core;

namespace App.Scanner;

/// <summary>
/// Cloud Service Scanner — detects OneDrive, Google Drive, Dropbox cache/sync leftovers.
/// Requirements: 7.1, 7.2, 7.3, 7.5, 7.6, 7.7
/// </summary>
public class CloudCacheScanner : IScanner
{
    public string Name => "Cloud Cache Scanner";
    public ScanType ScanType => ScanType.Deep;

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly List<CacheTarget> CloudTargets =
    [
        // OneDrive
        new("OneDrive logs", @"Microsoft\OneDrive\logs", LocalAppData,
            "OneDrive sync logs — safe to delete, tự tạo lại khi cần"),
        new("OneDrive setup", @"Microsoft\OneDrive\setup\downloads", LocalAppData,
            "OneDrive installer cache — có thể dọn"),
        // Google Drive
        new("Google DriveFS", @"Google\DriveFS", LocalAppData,
            "Google Drive sync metadata — cẩn thận, có thể cần re-sync"),
        new("Google Drive temp", @"Google\GoogleDrive\Temp", AppData,
            "Google Drive temp files — safe to delete"),
        // Dropbox
        new("Dropbox cache", @"Dropbox\.dropbox.cache", AppData,
            "Dropbox cache — safe to delete"),
        new("Dropbox instance", @"Dropbox\instance1", LocalAppData,
            "Dropbox instance data — có thể dọn nếu không sync issues"),
        // iCloud
        new("iCloud cache", @"Apple\iCloud\Cache", LocalAppData,
            "iCloud cache files — safe to delete"),
        new("iCloud Photos tmp", @"Apple\iCloud Photos\Downloads", LocalAppData,
            "iCloud Photos temp — có thể dọn, sẽ tải lại"),
    ];

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new ConcurrentBag<ScanItem>();
        int total = CloudTargets.Count;
        int done = 0;

        await Parallel.ForEachAsync(CloudTargets, new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = ct },
            (target, token) =>
        {
            try
            {
                var fullPath = Path.Combine(target.BasePath, target.RelativePath);
                if (!Directory.Exists(fullPath))
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                var size = GetDirSize(fullPath);
                if (size < 100_000) // < 100KB, bỏ qua
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                items.Add(new ScanItem
                {
                    Path = fullPath, Name = target.Name, SizeBytes = size, IsDirectory = true,
                    Category = ItemCategory.BrowserCache, Risk = RiskLevel.Low,
                    RecommendedAction = ItemAction.WarnDelete,
                    Suggestion = $"{target.Suggestion} ({ScanItem.FormatSize(size)})"
                });

                Interlocked.Increment(ref done);
                progress?.Report(($"Cloud: {target.Name}", done * 100 / total));
            }
            catch { Interlocked.Increment(ref done); }

            return ValueTask.CompletedTask;
        });

        var sorted = items.OrderByDescending(i => i.SizeBytes).ToList();
        progress?.Report(($"Found {sorted.Count} cloud caches ({ScanItem.FormatSize(sorted.Sum(i => i.SizeBytes))})", 100));
        return sorted;
    }

    private static long GetDirSize(string path)
    {
        try
        {
            long size = 0;
            foreach (var f in Directory.GetFiles(path, "*", new EnumerationOptions
            { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    private record CacheTarget(string Name, string RelativePath, string BasePath, string Suggestion);
}

/// <summary>
/// Browser Cache Scanner — Chrome, Firefox, Edge, Brave, Opera.
/// Requirements: 7.4, 7.8, 7.10
/// </summary>
public class BrowserCacheScanner : IScanner
{
    public string Name => "Browser Cache Scanner";
    public ScanType ScanType => ScanType.Developer;

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly List<BrowserTarget> BrowserTargets =
    [
        // Chrome
        new("Chrome Cache", @"Google\Chrome\User Data\Default\Cache", LocalAppData),
        new("Chrome Code Cache", @"Google\Chrome\User Data\Default\Code Cache", LocalAppData),
        new("Chrome GPU Cache", @"Google\Chrome\User Data\Default\GPUCache", LocalAppData),
        new("Chrome Service Worker", @"Google\Chrome\User Data\Default\Service Worker", LocalAppData),
        new("Chrome Media Cache", @"Google\Chrome\User Data\Default\Media Cache", LocalAppData),
        // Edge
        new("Edge Cache", @"Microsoft\Edge\User Data\Default\Cache", LocalAppData),
        new("Edge Code Cache", @"Microsoft\Edge\User Data\Default\Code Cache", LocalAppData),
        new("Edge GPU Cache", @"Microsoft\Edge\User Data\Default\GPUCache", LocalAppData),
        // Firefox
        new("Firefox Cache", @"Mozilla\Firefox\Profiles", LocalAppData, SubDir: "cache2"),
        // Brave
        new("Brave Cache", @"BraveSoftware\Brave-Browser\User Data\Default\Cache", LocalAppData),
        new("Brave Code Cache", @"BraveSoftware\Brave-Browser\User Data\Default\Code Cache", LocalAppData),
        // Opera
        new("Opera Cache", @"Opera Software\Opera Stable\Cache", AppData),
        // Chromium
        new("Chromium Cache", @"Chromium\User Data\Default\Cache", LocalAppData),
    ];

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new ConcurrentBag<ScanItem>();
        int total = BrowserTargets.Count;
        int done = 0;

        await Parallel.ForEachAsync(BrowserTargets, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            (target, token) =>
        {
            try
            {
                var fullPath = Path.Combine(target.BasePath, target.RelativePath);
                if (!Directory.Exists(fullPath))
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                // Handle Firefox profiles (need to find cache2 dir inside)
                if (target.SubDir != null)
                {
                    long totalSize = 0;
                    foreach (var profile in Directory.GetDirectories(fullPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        var cacheDir = Path.Combine(profile, target.SubDir);
                        if (Directory.Exists(cacheDir))
                            totalSize += GetSize(cacheDir);

                        // Also check startup cache
                        var startupCache = Path.Combine(profile, "startupCache");
                        if (Directory.Exists(startupCache))
                            totalSize += GetSize(startupCache);
                    }

                    if (totalSize > 100_000)
                    {
                        items.Add(new ScanItem
                        {
                            Path = fullPath, Name = target.Name, SizeBytes = totalSize, IsDirectory = true,
                            Category = ItemCategory.BrowserCache, Risk = RiskLevel.Safe,
                            RecommendedAction = ItemAction.SafeDelete,
                            Suggestion = $"Browser cache ({ScanItem.FormatSize(totalSize)}) — tự tạo lại"
                        });
                    }
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                var size = GetSize(fullPath);
                if (size < 50_000)
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                items.Add(new ScanItem
                {
                    Path = fullPath, Name = target.Name, SizeBytes = size, IsDirectory = true,
                    Category = ItemCategory.BrowserCache, Risk = RiskLevel.Safe,
                    RecommendedAction = ItemAction.SafeDelete,
                    Suggestion = $"Browser cache ({ScanItem.FormatSize(size)}) — tự tạo lại, mất dữ liệu duyệt web tạm thời"
                });

                Interlocked.Increment(ref done);
                progress?.Report(($"Browser: {target.Name}", done * 100 / total));
            }
            catch { Interlocked.Increment(ref done); }

            return ValueTask.CompletedTask;
        });

        var sorted = items.OrderByDescending(i => i.SizeBytes).ToList();
        progress?.Report(($"Found {sorted.Count} browser caches ({ScanItem.FormatSize(sorted.Sum(i => i.SizeBytes))})", 100));
        return sorted;
    }

    private static long GetSize(string path)
    {
        try
        {
            long size = 0;
            foreach (var f in Directory.GetFiles(path, "*", new EnumerationOptions
            { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 4 }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    private record BrowserTarget(string Name, string RelativePath, string BasePath, string? SubDir = null);
}

/// <summary>
/// Windows Store Scanner — detects Windows Store cache, packages, update cache.
/// Requirements: 9.1, 9.2, 9.3, 9.4, 9.7, 9.8, 9.9, 9.10
/// </summary>
public class WindowsStoreScanner : IScanner
{
    public string Name => "Windows Store Scanner";
    public ScanType ScanType => ScanType.Deep;

    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new List<ScanItem>();

        // 1. Windows Store cache
        var storeCache = Path.Combine(LocalAppData, "Packages");
        if (Directory.Exists(storeCache))
        {
            progress?.Report(("Scanning Windows Store packages...", 25));
            long storeSize = 0;
            int pkgCount = 0;

            try
            {
                foreach (var pkg in Directory.GetDirectories(storeCache, "Microsoft.*", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetDirectories(storeCache, "Windows.*", SearchOption.TopDirectoryOnly))
                    .Take(50))
                {
                    if (ct.IsCancellationRequested) break;
                    var size = GetSize(pkg);
                    var name = Path.GetFileName(pkg);

                    if (size > 1_000_000) // > 1MB
                    {
                        storeSize += size;
                        pkgCount++;
                    }
                }
            }
            catch { }

            if (storeSize > 10_000_000)
            {
                items.Add(new ScanItem
                {
                    Path = storeCache, Name = "Windows Store Cache", SizeBytes = storeSize, IsDirectory = true,
                    Category = ItemCategory.WindowsUpdateCache, Risk = RiskLevel.Medium,
                    RecommendedAction = ItemAction.WarnDelete,
                    Suggestion = $"Windows Store app cache ({ScanItem.FormatSize(storeSize)}, {pkgCount} packages) — an toàn nhưng một số app sẽ reset"
                });
            }
        }

        // 2. Windows Update cache
        var wuCache = Path.Combine(WinDir, "SoftwareDistribution", "Download");
        if (Directory.Exists(wuCache))
        {
            progress?.Report(("Scanning Windows Update cache...", 50));
            var wuSize = GetSize(wuCache);
            if (wuSize > 10_000_000)
            {
                items.Add(new ScanItem
                {
                    Path = wuCache, Name = "Windows Update Cache", SizeBytes = wuSize, IsDirectory = true,
                    Category = ItemCategory.WindowsUpdateCache, Risk = RiskLevel.Medium,
                    RecommendedAction = ItemAction.WarnDelete,
                    Suggestion = $"Windows Update files ({ScanItem.FormatSize(wuSize)}) — có thể dọn nếu không rollback updates"
                });
            }
        }

        // 3. Temp installer cache
        var tempCache = Path.Combine(WinDir, "Temp");
        if (Directory.Exists(tempCache))
        {
            progress?.Report(("Scanning Windows Temp...", 75));
            var tempSize = GetSize(tempCache);
            if (tempSize > 50_000_000)
            {
                items.Add(new ScanItem
                {
                    Path = tempCache, Name = "Windows Temp Files", SizeBytes = tempSize, IsDirectory = true,
                    Category = ItemCategory.TempFile, Risk = RiskLevel.Low,
                    RecommendedAction = ItemAction.SafeDelete,
                    Suggestion = $"Windows Temp ({ScanItem.FormatSize(tempSize)}) — an toàn để dọn"
                });
            }
        }

        progress?.Report(($"Found {items.Count} Windows cache items ({ScanItem.FormatSize(items.Sum(i => i.SizeBytes))})", 100));
        return Task.FromResult(items.OrderByDescending(i => i.SizeBytes).ToList());
    }

    private static long GetSize(string path)
    {
        try
        {
            long size = 0;
            foreach (var f in Directory.GetFiles(path, "*", new EnumerationOptions
            { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 3 }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }
}
