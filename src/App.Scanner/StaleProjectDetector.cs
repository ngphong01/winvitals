using System.Collections.Concurrent;
using System.Diagnostics;
using App.Core;

namespace App.Scanner;

/// <summary>
/// Stale Project Detector — finds abandoned dev projects and their reclaimable caches.
/// Checks git commit history and identifies node_modules, build/, etc. that can be safely deleted.
/// Requirements: Developer Cache v2.2
/// </summary>
public class StaleProjectDetector : IScanner
{
    public string Name => "Stale Project Detector";
    public ScanType ScanType => ScanType.Developer;

    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private const int StaleDaysThreshold = 60;

    /// <summary>
    /// Folders inside a project that are safe to delete if the project is stale.
    /// </summary>
    private static readonly string[] ReclaimableDirs =
        ["node_modules", "build", "dist", ".next", ".nuxt", ".output",
         "__pycache__", "target", ".gradle", "bin", "obj", ".dart_tool"];

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new ConcurrentBag<ScanItem>();

        // Discover project directories
        var projectDirs = DiscoverProjectDirectories();
        int total = projectDirs.Count;
        int done = 0;

        progress?.Report(($"Scanning {total} projects for staleness...", 0));

        await Parallel.ForEachAsync(projectDirs,
            new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct },
            (dir, token) =>
        {
            if (token.IsCancellationRequested) return ValueTask.CompletedTask;

            try
            {
                var info = AnalyzeProject(dir);
                Interlocked.Increment(ref done);

                if (info is { IsStale: true, ReclaimableBytes: > 1_048_576 })
                {
                    items.Add(new ScanItem
                    {
                        Path = dir,
                        Name = Path.GetFileName(dir),
                        SizeBytes = info.ReclaimableBytes,
                        IsDirectory = true,
                        Category = ItemCategory.DevCache,
                        Risk = RiskLevel.Low,
                        RecommendedAction = ItemAction.WarnDelete,
                        Suggestion = info.CommitsMonthsAgo > 0
                            ? $"Project stale ({info.LastCommitDaysAgo} ngày không commit). " +
                              $"Có thể dọn {ScanItem.FormatSize(info.ReclaimableBytes)} build caches."
                            : $"Không có git history. {ScanItem.FormatSize(info.ReclaimableBytes)} caches có thể dọn.",
                        LastModified = info.LastCommitDate,
                        AppOrigin = info.Framework
                    });

                    progress?.Report((
                        $"Stale: {info.Name} ({info.Framework}, {info.LastCommitDaysAgo}d)",
                        done * 100 / total));
                }
                else if (info is { ReclaimableBytes: > 10_485_760 }) // >10MB caches on active project
                {
                    items.Add(new ScanItem
                    {
                        Path = dir,
                        Name = Path.GetFileName(dir),
                        SizeBytes = info.ReclaimableBytes,
                        IsDirectory = true,
                        Category = ItemCategory.DevCache,
                        Risk = RiskLevel.Medium,
                        RecommendedAction = ItemAction.WarnDelete,
                        Suggestion = $"Active project ({info.Framework}). " +
                                      $"{ScanItem.FormatSize(info.ReclaimableBytes)} in build caches — " +
                                      $"có thể dọn và rebuild khi cần.",
                        AppOrigin = info.Framework
                    });
                }
            }
            catch { Interlocked.Increment(ref done); }

            return ValueTask.CompletedTask;
        });

        var sorted = items.OrderByDescending(i => i.SizeBytes).ToList();
        long totalReclaimable = sorted.Sum(i => i.SizeBytes);
        int staleCount = sorted.Count(i => i.Risk <= RiskLevel.Low);

        progress?.Report((
            $"Found {staleCount} stale projects, {sorted.Count} total. " +
            $"Reclaimable: {ScanItem.FormatSize(totalReclaimable)}",
            100));

        return sorted;
    }

    private static ProjectInfo? AnalyzeProject(string dir)
    {
        try
        {
            var name = Path.GetFileName(dir);
            var gitDir = Path.Combine(dir, ".git");
            var hasGit = Directory.Exists(gitDir);

            DateTime lastCommit = DateTime.MinValue;
            int daysAgo = 0;

            if (hasGit)
            {
                // Read git HEAD to get last commit date
                try
                {
                    var headFile = Path.Combine(gitDir, "logs", "HEAD");
                    if (File.Exists(headFile))
                    {
                        var lines = File.ReadAllLines(headFile);
                        if (lines.Length > 0)
                        {
                            var lastLine = lines[^1];
                            // Git log format: old_hash new_hash author email timestamp
                            // Timestamp is Unix epoch at position after email
                            var parts = lastLine.Split(' ');
                            if (parts.Length >= 5 && long.TryParse(parts[^1], out var unixTs))
                            {
                                lastCommit = DateTimeOffset.FromUnixTimeSeconds(unixTs).DateTime;
                                daysAgo = (int)(DateTime.Now - lastCommit).TotalDays;
                            }
                        }
                    }

                    // Fallback: check directory dates if log parsing failed
                    if (lastCommit == DateTime.MinValue)
                    {
                        var headPath = Path.Combine(gitDir, "HEAD");
                        if (File.Exists(headPath))
                            lastCommit = File.GetLastWriteTime(headPath);
                        daysAgo = (int)(DateTime.Now - lastCommit).TotalDays;
                    }
                }
                catch { /* git parsing failed, continue with date estimate */ }

                if (lastCommit == DateTime.MinValue)
                {
                    lastCommit = Directory.GetLastWriteTime(dir);
                    daysAgo = (int)(DateTime.Now - lastCommit).TotalDays;
                }
            }
            else
            {
                lastCommit = Directory.GetLastWriteTime(dir);
                daysAgo = (int)(DateTime.Now - lastCommit).TotalDays;
            }

            // Detect framework
            var framework = DetectFramework(dir);

            // Calculate reclaimable cache sizes
            long reclaimable = 0;
            foreach (var cacheDir in ReclaimableDirs)
            {
                var cachePath = Path.Combine(dir, cacheDir);
                if (Directory.Exists(cachePath))
                {
                    reclaimable += GetDirSizeFast(cachePath);
                }
            }

            bool isStale = daysAgo > StaleDaysThreshold;
            int monthsAgo = Math.Max(0, (int)(daysAgo / 30.0));

            return new ProjectInfo
            {
                Name = name,
                Path = dir,
                HasGit = hasGit,
                LastCommitDate = lastCommit,
                LastCommitDaysAgo = daysAgo,
                CommitsMonthsAgo = monthsAgo,
                IsStale = isStale,
                Framework = framework,
                ReclaimableBytes = reclaimable
            };
        }
        catch
        {
            return null;
        }
    }

    private static string DetectFramework(string dir)
    {
        var files = new HashSet<string>(Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f).ToLowerInvariant()));

        var subdirs = new HashSet<string>();
        try
        {
            foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                subdirs.Add(Path.GetFileName(d).ToLowerInvariant());
        }
        catch { }

        if (files.Contains("package.json"))
        {
            if (subdirs.Contains(".next")) return "Next.js";
            if (subdirs.Contains(".nuxt")) return "Nuxt";
            if (files.Contains("vite.config.ts") || files.Contains("vite.config.js")) return "Vite";
            if (files.Contains("angular.json")) return "Angular";
            return "Node.js";
        }
        if (files.Contains("cargo.toml")) return "Rust";
        if (files.Contains("go.mod")) return "Go";
        if (files.Contains("pom.xml")) return "Maven";
        if (files.Contains("build.gradle") || files.Contains("build.gradle.kts")) return "Gradle";
        if (files.Contains("requirements.txt") || files.Contains("pyproject.toml")) return "Python";
        if (files.Contains("pubspec.yaml")) return "Flutter/Dart";
        if (files.Contains("composer.json")) return "PHP";
        if (files.Contains("*.csproj") || files.Any(f => f.EndsWith(".csproj"))) return ".NET";
        if (files.Contains("mix.exs")) return "Elixir";
        if (files.Contains("makefile") || files.Contains("cmakelists.txt")) return "C/C++";
        if (files.Contains("gemfile")) return "Ruby";

        return "Unknown";
    }

    /// <summary>
    /// Discover potential project directories from common locations.
    /// </summary>
    private static List<string> DiscoverProjectDirectories()
    {
        var sources = new List<string>();
        var searchRoots = new[]
        {
            Path.Combine(UserProfile, "source"),
            Path.Combine(UserProfile, "projects"),
            Path.Combine(UserProfile, "dev"),
            Path.Combine(UserProfile, "Documents", "GitHub"),
            Path.Combine(UserProfile, "Documents", "GitLab"),
            Path.Combine(UserProfile, "source", "repos"),
            Path.Combine(UserProfile, "Desktop"),
        };

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    // Quick check: does it look like a project?
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(".")) continue; // skip hidden

                    // Check for known project markers
                    bool isProject = Directory.Exists(Path.Combine(dir, ".git")) ||
                                     File.Exists(Path.Combine(dir, "package.json")) ||
                                     File.Exists(Path.Combine(dir, "Cargo.toml")) ||
                                     File.Exists(Path.Combine(dir, "go.mod")) ||
                                     File.Exists(Path.Combine(dir, "pom.xml")) ||
                                     File.Exists(Path.Combine(dir, "pyproject.toml")) ||
                                     File.Exists(Path.Combine(dir, "composer.json"));

                    if (!isProject)
                    {
                        // Check one level deeper
                        try
                        {
                            foreach (var sub in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly).Take(5))
                            {
                                if (Directory.Exists(Path.Combine(sub, ".git")) ||
                                    File.Exists(Path.Combine(sub, "package.json")))
                                {
                                    sources.Add(sub);
                                }
                            }
                        }
                        catch { }
                        continue;
                    }

                    sources.Add(dir);
                }
            }
            catch { /* skip inaccessible root */ }
        }

        return sources.Distinct().ToList();
    }

    private static long GetDirSizeFast(string path)
    {
        try
        {
            long size = 0;
            foreach (var file in Directory.GetFiles(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 3
            }))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    private record ProjectInfo
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public bool HasGit { get; init; }
        public DateTime LastCommitDate { get; init; }
        public int LastCommitDaysAgo { get; init; }
        public int CommitsMonthsAgo { get; init; }
        public bool IsStale { get; init; }
        public string Framework { get; init; } = "Unknown";
        public long ReclaimableBytes { get; init; }
    }
}
