using App.Core;
using App.Storage;
using Serilog;

#pragma warning disable CS9113
namespace App.Cleaner;

/// <summary>
/// Helper dùng chung: di chuyển file/folder vào khu cách ly, ghi DB.
/// Tất cả cleaner phải dùng phương thức này — không bao giờ xóa thẳng.
/// </summary>
internal static class QuarantineHelper
{
    /// <summary>
    /// Di chuyển item vào thư mục quarantine và ghi vào DB.
    /// Trả về (true, qPath) nếu thành công, (false, "") nếu thất bại.
    /// </summary>
    public static async Task<(bool Success, string QuarantinePath)> SendToQuarantineAsync(
        ScanItem item,
        IStorageProvider storage,
        string sourceModule,
        RiskLevel risk)
    {
        try
        {
            // Thư mục quarantine: %LocalAppData%\WindowsHealthManager\quarantine\
            var qBase = DatabaseProvider.GetQuarantineDirectory();
            Directory.CreateDirectory(qBase);

            // Tên file duy nhất để tránh trùng
            var safeName = Path.GetFileName(item.Path)
                .Replace('\\', '_').Replace('/', '_');
            var qPath = Path.Combine(qBase, $"{Guid.NewGuid():N}_{safeName}");

            if (File.Exists(item.Path))
                File.Move(item.Path, qPath, overwrite: false);
            else if (Directory.Exists(item.Path))
                Directory.Move(item.Path, qPath);
            else
                return (false, ""); // path không tồn tại

            await storage.SaveQuarantineItemAsync(new QuarantineItem
            {
                OriginalPath  = item.Path,
                QuarantinePath = qPath,
                FileName      = item.Name,
                SizeBytes     = item.SizeBytes,
                QuarantineDate = DateTime.Now,
                ExpiryDate    = DateTime.Now.AddDays(14),
                Status        = QuarantineStatus.Active,
                Reason        = string.IsNullOrWhiteSpace(item.Suggestion)
                                    ? $"Dọn bởi {sourceModule}"
                                    : item.Suggestion,
                SourceModule  = sourceModule,
                Risk          = risk
            });

            Log.Information("[{Module}] Quarantined {Path} → {QPath} ({Size})",
                sourceModule, item.Path, qPath, ScanItem.FormatSize(item.SizeBytes));

            return (true, qPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{Module}] Failed to quarantine {Path}", sourceModule, item.Path);
            return (false, "");
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// QUICK CLEANER
// Temp, Recycle Bin, Logs, Crash Dumps, Prefetch, Thumbnails
// → Toàn bộ đưa vào quarantine 14 ngày, không xóa thẳng
// ═══════════════════════════════════════════════════════════════
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
            progress?.Report($"Đang đưa vào cách ly: {item.Name}");

            try
            {
                var (action, risk, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);

                // Bảo vệ tuyệt đối — skip nếu bị block
                if (action == ItemAction.Block || riskEngine.IsProtected(item.Path))
                {
                    Log.Information("[QuickCleaner] SKIPPED protected: {Path}", item.Path);
                    continue;
                }

                // Đưa vào quarantine thay vì xóa thẳng
                var (ok, _) = await QuarantineHelper.SendToQuarantineAsync(item, storage, "QuickCleaner", risk);
                if (ok)
                {
                    freed += item.SizeBytes;
                    processed++;
                }
                else
                {
                    errors.Add(item.Path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Path}: {ex.Message}");
            }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate     = DateTime.Now,
            CleanLevel    = CleanLevel.Quick,
            ItemsCleaned  = processed,
            SpaceFreedBytes = freed,
            ItemsInQuarantine = processed  // tất cả đều vào quarantine
        });

        return (freed, processed, errors);
    }
}

// ═══════════════════════════════════════════════════════════════
// DEEP CLEANER
// Windows Update cache, App leftovers, Old installers, Orphans
// → Tất cả đưa vào quarantine, không xóa thẳng
// ═══════════════════════════════════════════════════════════════
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

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report($"Đang đưa vào cách ly: {item.Name}");

            try
            {
                var (action, risk, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);

                if (action == ItemAction.Block || riskEngine.IsProtected(item.Path))
                {
                    Log.Information("[DeepCleaner] SKIPPED protected: {Path}", item.Path);
                    continue;
                }

                // Fail-safe: không xóa thư mục gốc project
                if (Directory.Exists(item.Path) && DeveloperCleaner.IsProjectRootDirectory(item.Path))
                {
                    Log.Warning("[DeepCleaner] BLOCKED project root: {Path}", item.Path);
                    continue;
                }

                // TẤT CẢ đều vào quarantine — không phân biệt risk level
                var (ok, _) = await QuarantineHelper.SendToQuarantineAsync(item, storage, "DeepCleaner", risk);
                if (ok)
                {
                    freed += item.SizeBytes;
                    processed++;
                }
                else
                {
                    errors.Add(item.Path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Path}: {ex.Message}");
            }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate     = DateTime.Now,
            CleanLevel    = CleanLevel.Deep,
            ItemsCleaned  = processed,
            SpaceFreedBytes = freed,
            ItemsInQuarantine = processed
        });

        return (freed, processed, errors);
    }
}

// ═══════════════════════════════════════════════════════════════
// DEVELOPER CLEANER
// node_modules, dist, build, .next, .gradle, __pycache__, v.v.
// → Tất cả đưa vào quarantine, không xóa thẳng
// ═══════════════════════════════════════════════════════════════
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
            progress?.Report($"Đang đưa vào cách ly: {item.Name}");

            try
            {
                var (action, risk, _) = ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);
                if (action == ItemAction.Block || riskEngine.IsProtected(item.Path)) continue;

                // Fail-safe: không bao giờ xóa thư mục gốc project
                if (IsProjectRootDirectory(item.Path))
                {
                    Log.Warning("[DeveloperCleaner] BLOCKED project root: {Path}", item.Path);
                    continue;
                }

                // Đưa toàn bộ thư mục cache vào quarantine
                var (ok, _) = await QuarantineHelper.SendToQuarantineAsync(item, storage, "DeveloperCleaner", risk);
                if (ok)
                {
                    freed += item.SizeBytes;
                    processed++;
                }
                else
                {
                    errors.Add(item.Path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Path}: {ex.Message}");
            }
        }

        await storage.SaveCleanHistoryAsync(new CleanHistory
        {
            CleanDate     = DateTime.Now,
            CleanLevel    = CleanLevel.Developer,
            ItemsCleaned  = processed,
            SpaceFreedBytes = freed,
            ItemsInQuarantine = processed
        });

        return (freed, processed, errors);
    }

    public static bool IsDevCacheDir(string dirName) =>
        DevCacheDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase);

    public static bool IsProjectRootDirectory(string path)
    {
        if (!Directory.Exists(path)) return false;
        try
        {
            if (Directory.Exists(Path.Combine(path, ".git")))                                  return true;
            if (File.Exists(Path.Combine(path, "package.json")))                               return true;
            if (File.Exists(Path.Combine(path, "Cargo.toml")))                                 return true;
            if (File.Exists(Path.Combine(path, "go.mod")))                                     return true;
            if (File.Exists(Path.Combine(path, "pom.xml")))                                    return true;
            if (File.Exists(Path.Combine(path, "pyproject.toml")))                             return true;
            if (Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0) return true;
            if (Directory.GetFiles(path, "*.sln",    SearchOption.TopDirectoryOnly).Length > 0) return true;
        }
        catch { }
        return false;
    }
}
