using App.Core;
using Serilog;

namespace App.Cleaner;

/// <summary>
/// Quick Cleaner: Temp, Recycle Bin, Logs, Crash Dumps, Prefetch, Thumbnails
/// </summary>
public class QuickCleaner(IRuleEngine ruleEngine, IRiskEngine riskEngine, IStorageProvider storage)
    : ICleaner
{
    public string Name => "Quick Cleaner";
    public CleanLevel CleanLevel => CleanLevel.Quick;

    public async Task<(long FreedBytes, int ItemsProcessed, List<string> Errors)> CleanAsync(
        IEnumerable<ScanItem> items, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        int processed = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Cleaning: {item.Name}");

            try
            {
                var (action, _, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);
                if (action == ItemAction.Block) continue;

                if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                    Log.Information("[QuickClean] Deleted {Path} ({Size})", item.Path, ScanItem.FormatSize(item.SizeBytes));
                    freed += item.SizeBytes;
                    processed++;
                }
                else if (Directory.Exists(item.Path))
                {
                    // Only delete contents, preserve root for system dirs
                    foreach (var file in Directory.GetFiles(item.Path, "*.*", SearchOption.AllDirectories))
                    {
                        if (ct.IsCancellationRequested) break;
                        try { File.Delete(file); } catch { errors.Add(file); }
                    }
                    freed += item.SizeBytes;
                    processed++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Path}: {ex.Message}");
            }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Quick,
            ItemsCleaned = processed,
            SpaceFreedBytes = freed
        });

        return (freed, processed, errors);
    }
}

/// <summary>
/// Deep Cleaner: Windows Update cache, App leftovers, Old installers, Unknown folders
/// </summary>
public class DeepCleaner(IRuleEngine ruleEngine, IRiskEngine riskEngine, IStorageProvider storage)
    : ICleaner
{
    public string Name => "Deep Cleaner";
    public CleanLevel CleanLevel => CleanLevel.Deep;

    public async Task<(long FreedBytes, int ItemsProcessed, List<string> Errors)> CleanAsync(
        IEnumerable<ScanItem> items, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        int processed = 0;
        var errors = new List<string>();
        var quarantined = 0;

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Processing: {item.Name}");
            var (action, risk, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);

            if (action == ItemAction.Block) continue;

            try
            {
                if (action == ItemAction.Quarantine || risk >= RiskLevel.High)
                {
                    // Move to quarantine instead of direct delete
                    var qPath = Path.Combine(
                        AppContext.BaseDirectory,
                        "quarantine", $"{Guid.NewGuid():N}_{item.Name}");
                    Directory.CreateDirectory(Path.GetDirectoryName(qPath)!);
                    if (File.Exists(item.Path))
                        File.Move(item.Path, qPath);
                    else if (Directory.Exists(item.Path))
                        Directory.Move(item.Path, qPath);

                    await storage.SaveQuarantineItemAsync(new QuarantineItem
                    {
                        OriginalPath = item.Path,
                        QuarantinePath = qPath,
                        FileName = item.Name,
                        SizeBytes = item.SizeBytes,
                        QuarantineDate = DateTime.Now,
                        ExpiryDate = DateTime.Now.AddDays(14),
                        Status = QuarantineStatus.Active,
                        Reason = item.Suggestion,
                        SourceModule = "DeepCleaner",
                        Risk = risk
                    });
                    quarantined++;
                    freed += item.SizeBytes;
                    processed++;
                }
                else
                {
                    if (Directory.Exists(item.Path))
                        Directory.Delete(item.Path, true);
                    else if (File.Exists(item.Path))
                        File.Delete(item.Path);
                    freed += item.SizeBytes;
                    processed++;
                }
            }
            catch (Exception ex) { errors.Add($"{item.Path}: {ex.Message}"); }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Deep,
            ItemsCleaned = processed,
            SpaceFreedBytes = freed,
            ItemsInQuarantine = quarantined
        });

        return (freed, processed, errors);
    }
}

/// <summary>
/// Developer Cleaner: node_modules, dist, build, .next, .gradle, __pycache__, etc.
/// </summary>
public class DeveloperCleaner(IRuleEngine ruleEngine, IRiskEngine riskEngine, IStorageProvider storage)
    : ICleaner
{
    public string Name => "Developer Cleaner";
    public CleanLevel CleanLevel => CleanLevel.Developer;

    private static readonly string[] DevCacheDirs =
    [
        "node_modules", "build", "dist", ".next", ".nuxt", ".output",
        ".gradle", "__pycache__", ".pytest_cache", ".mypy_cache",
        ".ruff_cache", ".dart_tool", ".flutter-plugins", "target",
        "coverage", ".nyc_output", ".terraform", ".cache", "Pods", "vendor"
    ];

    public async Task<(long FreedBytes, int ItemsProcessed, List<string> Errors)> CleanAsync(
        IEnumerable<ScanItem> items, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        long freed = 0;
        int processed = 0;
        var errors = new List<string>();

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Removing: {item.Name}");

            try
            {
                var (action, _, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);
                if (action == ItemAction.Block) continue;

                if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, true);
                    freed += item.SizeBytes;
                    processed++;
                }
            }
            catch (Exception ex) { errors.Add($"{item.Path}: {ex.Message}"); }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Developer,
            ItemsCleaned = processed,
            SpaceFreedBytes = freed
        });

        return (freed, processed, errors);
    }

    public static bool IsDevCacheDir(string dirName) =>
        DevCacheDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase);
}
