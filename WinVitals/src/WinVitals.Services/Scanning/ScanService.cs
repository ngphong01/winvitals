using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;
using WinVitals.Core.Storage;

namespace WinVitals.Services.Scanning;

public sealed class ScanService : IScanService
{
    private readonly RuleRepository _repo;
    private readonly SettingsStore _settings;
    private readonly ILogger<ScanService> _log;

    private RuleEvaluator? _evaluator;
    private readonly object _evalLock = new();

    public ScanService(RuleRepository repo, SettingsStore settings, ILogger<ScanService> log)
    {
        _repo = repo;
        _settings = settings;
        _log = log;
        _repo.Changed += (_, _) =>
        {
            lock (_evalLock) _evaluator = null;
            _log.LogInformation("Rules changed - evaluator invalidated");
        };
    }

    private RuleEvaluator Evaluator
    {
        get
        {
            lock (_evalLock)
            {
                if (_evaluator == null)
                {
                    var rules = _repo.LoadAll();
                    _evaluator = new RuleEvaluator(rules);
                    _log.LogInformation("Evaluator built with {Count} rules", _evaluator.RuleCount);
                }
                return _evaluator;
            }
        }
    }

    public async IAsyncEnumerable<ScanItem> ScanAsync(
        ScanPreset preset,
        IProgress<ScanProgress>? progress,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var roots = GetRootsFor(preset);

        // Force fresh evaluator to pick up latest rules
        lock (_evalLock) _evaluator = null;
        var evaluator = Evaluator;

        var settings = _settings.Get();
        var exclusions = new ExclusionFilter(settings.ExcludedPatterns ?? new List<string>());

        _log.LogInformation(
            "Scan starting: preset={Preset}, roots=[{Roots}], rules={RuleCount}, exclusions={ExCount}",
            preset,
            string.Join(", ", roots),
            evaluator.RuleCount,
            settings.ExcludedPatterns?.Count ?? 0);

        if (evaluator.RuleCount == 0)
        {
            _log.LogWarning("No rules loaded! Scan will only surface items in Deep mode.");
        }

        int found = 0;
        int skipped = 0;
        long bytes = 0;
        var lastReport = DateTime.UtcNow;

        int maxDepth = preset switch
        {
            ScanPreset.Quick => 2,
            ScanPreset.Deep => 4,
            ScanPreset.Developer => 3,
            _ => 3
        };

        progress?.Report(new ScanProgress("Starting…", 0, 0));

        foreach (var root in roots)
        {
            if (ct.IsCancellationRequested) break;

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                _log.LogWarning("Root directory not found or invalid: {Root}", root);
                continue;
            }

            _log.LogInformation("Scanning root: {Root} (maxDepth={Depth})", root, maxDepth);
            progress?.Report(new ScanProgress($"Scanning {root}…", found, bytes));

            // Yield control so UI thread can breathe between roots
            await Task.Yield();

            foreach (var file in SafeEnumerate(root, ct, maxDepth))
            {
                if (ct.IsCancellationRequested) break;

                if (exclusions.IsExcluded(file))
                {
                    skipped++;
                    continue;
                }

                ScanItem? item = TryBuildItem(file, evaluator, preset);
                if (item == null)
                {
                    skipped++;
                    continue;
                }

                found++;
                bytes += item.SizeBytes;

                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds > 200)
                {
                    var dir = Path.GetDirectoryName(item.Path) ?? "";
                    progress?.Report(new ScanProgress(dir, found, bytes));
                    lastReport = now;
                }

                yield return item;
            }
        }

        _log.LogInformation(
            "Scan complete: found={Found}, skipped={Skipped}, bytes={Bytes}, cancelled={Cancelled}",
            found, skipped, bytes, ct.IsCancellationRequested);

        progress?.Report(new ScanProgress("Done", found, bytes));
    }

    /// <summary>
    /// Build a ScanItem from a file path, or return null if it should be skipped.
    /// Isolated so we can safely try/catch around FileInfo access (which throws on long paths, etc.).
    /// </summary>
    private ScanItem? TryBuildItem(string file, RuleEvaluator evaluator, ScanPreset preset)
    {
        try
        {
            var fi = new FileInfo(file);
            if (!fi.Exists) return null;

            var match = evaluator.Evaluate(fi.FullName, fi.Length, fi.LastWriteTimeUtc);

            // If no specific rule matched, only surface in Deep scan
            if (match.RuleId == "default" && preset != ScanPreset.Deep)
                return null;

            var category = CategoryClassifier.Classify(match.RuleId, fi.FullName, fi.Extension);

            return new ScanItem(
                fi.FullName,
                fi.Length,
                fi.LastWriteTimeUtc,
                category,
                match.Risk,
                match.Action,
                match.RuleId);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Skipping file due to error: {File}", file);
            return null;
        }
    }

    private static string[] GetRootsFor(ScanPreset preset)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // Deduplicate + drop empty entries defensively
        IEnumerable<string> raw = preset switch
        {
            ScanPreset.Quick => new[]
            {
                Path.GetTempPath(),
                Path.Combine(windir, "Temp"),
                Path.Combine(windir, "Prefetch"),
                Path.Combine(localAppData, "Microsoft", "Windows", "Explorer"),
                Path.Combine(localAppData, "Google", "Chrome", "User Data"),
                Path.Combine(localAppData, "Microsoft", "Edge", "User Data")
            },
            ScanPreset.Deep => new[]
            {
                Path.GetTempPath(),
                Path.Combine(windir, "Temp"),
                Path.Combine(windir, "SoftwareDistribution", "Download"),
                Path.Combine(userProfile, "Downloads"),
                localAppData
            },
            ScanPreset.Developer => new[]
            {
                Path.Combine(userProfile, "source"),
                Path.Combine(userProfile, "projects"),
                Path.Combine(userProfile, "workspace"),
                Path.Combine(userProfile, "repos"),
                Path.Combine(localAppData, "NuGet", "v3-cache"),
                Path.Combine(localAppData, "Microsoft", "VisualStudio")
            },
            _ => Array.Empty<string>()
        };

        return raw
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> SafeEnumerate(string root, CancellationToken ct, int maxDepth)
    {
        var stack = new Stack<(string path, int depth)>();
        stack.Push((root, 0));

        const int maxFilesPerDir = 5000;
        const int maxTotalFiles = 50000;
        int totalFiles = 0;

        while (stack.Count > 0)
        {
            if (ct.IsCancellationRequested) yield break;
            if (totalFiles >= maxTotalFiles) yield break;

            var (dir, depth) = stack.Pop();

            // --- Enumerate files (bounded, no yield inside try) ---
            string[] files = TryGetFiles(dir);

            int count = 0;
            foreach (var f in files)
            {
                if (ct.IsCancellationRequested) yield break;
                if (++count > maxFilesPerDir) break;

                yield return f;
                totalFiles++;

                if (totalFiles >= maxTotalFiles) yield break;
            }

            if (depth >= maxDepth) continue;

            // --- Enumerate subdirectories ---
            string[] subdirs = TryGetDirectories(dir);
            foreach (var s in subdirs)
            {
                if (ShouldSkipDirectory(s)) continue;
                stack.Push((s, depth + 1));
            }
        }
    }

    private static string[] TryGetFiles(string dir)
    {
        try { return Directory.GetFiles(dir); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (DirectoryNotFoundException) { return Array.Empty<string>(); }
        catch (PathTooLongException) { return Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    private static string[] TryGetDirectories(string dir)
    {
        try { return Directory.GetDirectories(dir); }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (DirectoryNotFoundException) { return Array.Empty<string>(); }
        catch (PathTooLongException) { return Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    private static bool ShouldSkipDirectory(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);

            // Skip reparse points (junctions/symlinks) to avoid loops
            if ((attrs & FileAttributes.ReparsePoint) != 0) return true;

            // Skip common noisy/system directories by name
            var name = Path.GetFileName(path);
            if (string.Equals(name, "$Recycle.Bin", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, "System Volume Information", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }
        catch
        {
            return true; // if we can't inspect it, don't recurse
        }
    }
}
