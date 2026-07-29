using System.Collections.Concurrent;
using App.Core;

namespace App.Scanner;

/// <summary>
/// Developer Cache Scanner — finds and evaluates package manager caches.
/// Safe to delete: npm, pip, NuGet, Cargo, Go modules, Docker, Composer, Gradle, Maven.
/// Requirements: Developer Cache v2.1
/// </summary>
public class DevCacheScanner : IScanner
{
    public string Name => "Package Cache Analyzer";
    public ScanType ScanType => ScanType.Developer;

    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// Known package cache locations with safe-deletion metadata.
    /// </summary>
    private static readonly List<CacheTarget> Targets =
    [
        // Node.js
        new() { Name = "npm cache", Path = Path.Combine(AppData, "npm-cache"), Category = ItemCategory.DevCache,
            Suggestion = "npm cache — dọn an toàn, tự tạo lại khi chạy npm install", Tool = "npm cache clean --force" },
        new() { Name = "Yarn cache", Path = Path.Combine(LocalAppData, "Yarn"), Category = ItemCategory.DevCache,
            Suggestion = "Yarn cache — dọn bằng yarn cache clean", Tool = "yarn cache clean" },
        new() { Name = "pnpm store", Path = Path.Combine(LocalAppData, "pnpm"), Category = ItemCategory.DevCache,
            Suggestion = "pnpm store — dọn bằng pnpm store prune", Tool = "pnpm store prune" },

        // Python
        new() { Name = "pip cache", Path = Path.Combine(LocalAppData, "pip", "cache"), Category = ItemCategory.DevCache,
            Suggestion = "pip cache — dọn bằng pip cache purge", Tool = "pip cache purge" },
        new() { Name = "uv cache", Path = Path.Combine(LocalAppData, "uv", "cache"), Category = ItemCategory.DevCache,
            Suggestion = "uv cache — dọn bằng uv cache clean", Tool = "uv cache clean" },
        new() { Name = "Conda pkgs", Path = Path.Combine(UserProfile, "miniconda3", "pkgs"), Category = ItemCategory.DevCache,
            Suggestion = "Conda packages cache — dọn bằng conda clean -a", Tool = "conda clean -a" },

        // .NET / NuGet
        new() { Name = "NuGet cache", Path = Path.Combine(UserProfile, ".nuget", "packages"), Category = ItemCategory.DevCache,
            Suggestion = "NuGet package cache — dọn bằng dotnet nuget locals all --clear", Tool = "dotnet nuget locals all --clear" },
        new() { Name = "NuGet HTTP cache", Path = Path.Combine(LocalAppData, "NuGet", "v3-cache"), Category = ItemCategory.DevCache,
            Suggestion = "NuGet HTTP cache — dọn bằng dotnet nuget locals http-cache --clear", Tool = "dotnet nuget locals http-cache --clear" },
        new() { Name = ".NET temp", Path = Path.Combine(LocalAppData, "Microsoft", "dotnet"), Category = ItemCategory.DevCache,
            Suggestion = ".NET SDK temporary files — safe to clean", Tool = null },

        // Rust
        new() { Name = "Cargo registry", Path = Path.Combine(UserProfile, ".cargo", "registry"), Category = ItemCategory.DevCache,
            Suggestion = "Cargo registry cache — tự tải lại khi cargo build", Tool = "cargo cache -a" },
        new() { Name = "Cargo target (global)", Path = Path.Combine(UserProfile, ".cargo", "target"), Category = ItemCategory.DevCache,
            Suggestion = "Cargo global target dir — an toàn nếu project có target/ riêng", Tool = null },

        // Go
        new() { Name = "Go module cache", Path = Path.Combine(UserProfile, "go", "pkg", "mod"), Category = ItemCategory.DevCache,
            Suggestion = "Go module cache — dọn bằng go clean -modcache", Tool = "go clean -modcache" },
        new() { Name = "Go build cache", Path = Path.Combine(UserProfile, "go", "pkg", "obj"), Category = ItemCategory.DevCache,
            Suggestion = "Go build cache — dọn bằng go clean -cache", Tool = "go clean -cache" },

        // Java / JVM
        new() { Name = "Gradle cache", Path = Path.Combine(UserProfile, ".gradle", "caches"), Category = ItemCategory.DevCache,
            Suggestion = "Gradle dependency cache — tự tải lại khi build", Tool = "gradle cleanBuildCache" },
        new() { Name = "Maven local repo", Path = Path.Combine(UserProfile, ".m2", "repository"), Category = ItemCategory.DevCache,
            Suggestion = "Maven local repo — cân nhắc giữ nếu build offline thường xuyên", Tool = null },
        new() { Name = "sbt cache", Path = Path.Combine(UserProfile, ".sbt"), Category = ItemCategory.DevCache,
            Suggestion = "Scala sbt cache — safe to clean", Tool = null },
        new() { Name = "Coursier cache", Path = Path.Combine(LocalAppData, "Coursier"), Category = ItemCategory.DevCache,
            Suggestion = "Coursier cache (Scala tools) — safe to clean", Tool = null },

        // PHP
        new() { Name = "Composer cache", Path = Path.Combine(AppData, "Composer"), Category = ItemCategory.DevCache,
            Suggestion = "Composer cache — dọn bằng composer clear-cache", Tool = "composer clear-cache" },

        // Flutter / Dart
        new() { Name = "Pub cache", Path = Path.Combine(AppData, "Pub", "Cache"), Category = ItemCategory.DevCache,
            Suggestion = "Dart Pub cache — dọn bằng dart pub cache clean", Tool = "dart pub cache clean" },
        new() { Name = "Dart tool", Path = Path.Combine(UserProfile, ".dart-tool"), Category = ItemCategory.DevCache,
            Suggestion = "Dart tool cache — safe to clean", Tool = null },

        // Docker (Windows)
        new() { Name = "Docker data", Path = Path.Combine(LocalAppData, "Docker"), Category = ItemCategory.DevCache,
            Suggestion = "Docker data — dùng Docker Desktop: Troubleshoot > Clean / Purge data", Tool = "docker system prune -af" },
        new() { Name = "Docker Desktop cache", Path = Path.Combine(AppData, "Docker"), Category = ItemCategory.DevCache,
            Suggestion = "Docker Desktop logs + cache — safe to clean", Tool = null },

        // Android
        new() { Name = "Android Gradle", Path = Path.Combine(UserProfile, ".android", "cache"), Category = ItemCategory.DevCache,
            Suggestion = "Android build cache — safe to clean", Tool = null },
        new() { Name = "Android SDK tmp", Path = Path.Combine(LocalAppData, "Android", "Sdk", ".temp"), Category = ItemCategory.DevCache,
            Suggestion = "Android SDK temp files — safe to clean", Tool = null },
    ];

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new ConcurrentBag<ScanItem>();
        int total = Targets.Count;
        int done = 0;

        await Parallel.ForEachAsync(Targets, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            (target, token) =>
        {
            if (token.IsCancellationRequested) return ValueTask.CompletedTask;

            try
            {
                if (!Directory.Exists(target.Path))
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                var size = GetDirectorySizeFast(target.Path);
                if (size < 1_048_576) // < 1MB
                {
                    Interlocked.Increment(ref done);
                    return ValueTask.CompletedTask;
                }

                items.Add(new ScanItem
                {
                    Path = target.Path,
                    Name = target.Name,
                    SizeBytes = size,
                    IsDirectory = true,
                    Category = target.Category,
                    Risk = RiskLevel.Low,
                    RecommendedAction = ItemAction.WarnDelete,
                    Suggestion = $"{target.Suggestion} ({ScanItem.FormatSize(size)})"
                });

                Interlocked.Increment(ref done);
                progress?.Report(($"Analyzing: {target.Name} ({ScanItem.FormatSize(size)})", done * 100 / total));
            }
            catch { Interlocked.Increment(ref done); }

            return ValueTask.CompletedTask;
        });

        var sorted = items.OrderByDescending(i => i.SizeBytes).ToList();
        progress?.Report(($"Found {sorted.Count} package caches ({ScanItem.FormatSize(sorted.Sum(i => i.SizeBytes))})", 100));
        return sorted;
    }

    private static long GetDirectorySizeFast(string path)
    {
        try
        {
            long size = 0;
            foreach (var file in Directory.GetFiles(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MaxRecursionDepth = 4
            }))
            {
                try { size += new FileInfo(file).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    private record CacheTarget
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public ItemCategory Category { get; init; }
        public string Suggestion { get; init; } = "";
        public string? Tool { get; init; }
    }
}
