using System.Collections.Concurrent;
using App.Core;

namespace App.Scanner;

/// <summary>
/// Disk Scanner — folder-level only, ultra-fast streaming enumeration.
/// Reports directory sizes. Individual files handled by LargeFileFinder.
/// </summary>
public class DiskScanner(IRuleEngine ruleEngine, IRiskEngine riskEngine) : IScanner
{
    public string Name => "Disk Scanner";
    public ScanType ScanType => ScanType.Quick;

    // Chỉ skip system/protected dirs — KHÔNG skip dev cache dirs
    // (Developer Cleaner cần tìm node_modules, build, dist, .next, ...)
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN", "System Volume Information", "Windows",
        ".git", ".vs", ".idea"
    };

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new ConcurrentBag<ScanItem>();
        var driveList = drives.ToList();
        int doneCount = 0;

        await Parallel.ForEachAsync(driveList, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(2, driveList.Count),
            CancellationToken = ct
        }, async (drive, token) =>
        {
            if (!Directory.Exists(drive)) return;
            progress?.Report(($"Scanning {drive}...", 0));

            await Task.Run(() =>
            {
                var driveEntry = ScanFolderFast(drive, drive, 0, 4, results, token);
                results.Add(driveEntry);

                Interlocked.Increment(ref doneCount);
                progress?.Report(($"Done {drive}", doneCount * 100 / driveList.Count));
            }, token);
        });

        var sorted = results.OrderByDescending(i => i.SizeBytes).ToList();
        progress?.Report(($"Found {sorted.Count} items.", 100));
        return sorted;
    }

    /// <summary>
    /// Recursive folder scanner — reports each significant folder as a ScanItem
    /// into the shared results bag, and returns its own ScanItem for parent use.
    /// </summary>
    private ScanItem ScanFolderFast(string root, string path, int depth, int maxDepth,
        ConcurrentBag<ScanItem> results, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || depth > maxDepth)
            return new ScanItem { Path = path, Name = Path.GetFileName(path) ?? path, IsDirectory = true };

        long totalSize = 0;
        string[] entries;

        try { entries = Directory.GetFileSystemEntries(path); }
        catch { return new ScanItem { Path = path, Name = Path.GetFileName(path) ?? path, IsDirectory = true, SizeBytes = 0 }; }

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                if (Directory.Exists(entry))
                {
                    var dirName = Path.GetFileName(entry);
                    if (SkipDirs.Contains(dirName)) continue;

                    var child = ScanFolderFast(root, entry, depth + 1, maxDepth, results, ct);
                    totalSize += child.SizeBytes;

                    // Report top-level folders (depth=1) into results
                    if (child.SizeBytes >= 50 * 1024 * 1024 && depth == 1)
                        results.Add(child);
                }
                else
                {
                    totalSize += new FileInfo(entry).Length;
                }
            }
            catch { /* skip inaccessible */ }
        }

        var dirInfo = new DirectoryInfo(path);
        var category = ClassifyFolder(Path.GetFileName(path));
        var (action, risk, rule) = ruleEngine.Evaluate(path, totalSize, dirInfo.LastWriteTime);
        if (risk == RiskLevel.Unknown)
            risk = riskEngine.AssessRisk(path, category, totalSize, dirInfo.LastWriteTime);

        return new ScanItem
        {
            Path = path,
            Name = Path.GetFileName(path) ?? path,
            SizeBytes = totalSize,
            IsDirectory = true,
            Category = category,
            Risk = risk,
            RecommendedAction = action,
            MatchedRule = rule,
            LastModified = dirInfo.LastWriteTime,
            Suggestion = GetFolderSuggestion(category, totalSize, Path.GetFileName(path))
        };
    }

    private static ItemCategory ClassifyFolder(string name)
    {
        if (string.IsNullOrEmpty(name)) return ItemCategory.Unknown;
        return name.ToLowerInvariant() switch
        {
            "node_modules" or "vendor" => ItemCategory.DevCache,
            "build" or "dist" or "out" => ItemCategory.DevCache,
            ".next" or ".nuxt" or ".output" => ItemCategory.DevCache,
            ".gradle" => ItemCategory.DevCache,
            "__pycache__" => ItemCategory.DevCache,
            ".dart_tool" => ItemCategory.DevCache,
            ".terraform" => ItemCategory.DevCache,
            "obj" or "bin" => ItemCategory.DevCache,
            "temp" or "tmp" => ItemCategory.TempFile,
            "$recycle.bin" => ItemCategory.RecycleBin,
            "windows" => ItemCategory.TempFile,
            "program files" or "program files (x86)" => ItemCategory.Unknown,
            "users" or "documents and settings" => ItemCategory.Unknown,
            "perflogs" or "intel" or "recovery" => ItemCategory.TempFile,
            _ => ItemCategory.Unknown
        };
    }

    private static string GetFolderSuggestion(ItemCategory cat, long size, string name = "")
    {
        var formatted = ScanItem.FormatSize(size);
        var lower = name.ToLowerInvariant();

        if (lower is "program files" or "program files (x86)")
            return $"Installed apps ({formatted}) — không nên xóa thủ công, dùng Control Panel để gỡ";
        if (lower is "windows")
            return "Windows system files — không xóa";
        if (lower is "users")
            return $"Dữ liệu người dùng ({formatted}) — cẩn thận khi xóa";
        if (lower is "perflogs" or "intel" or "recovery")
            return $"Có thể dọn ({formatted}) — an toàn nếu không cần recovery";

        return cat switch
        {
            ItemCategory.DevCache => $"Dev cache ({formatted}) — dọn được, sẽ tạo lại khi cần",
            ItemCategory.TempFile => $"Temp ({formatted}) — dọn an toàn",
            ItemCategory.RecycleBin => "Recycle Bin — dọn an toàn",
            ItemCategory.Unknown when size > 5_000_000_000 => $"Thư mục lớn ({formatted}) — kiểm tra trước khi xóa",
            _ => $"({formatted}) — kiểm tra trước khi xóa"
        };
    }
}

/// <summary>
/// Large File Finder - tìm file lớn hơn ngưỡng (tối ưu: skip system dirs, báo progress)
/// </summary>
public class LargeFileFinder(IRuleEngine ruleEngine, IRiskEngine riskEngine) : IScanner
{
    public string Name => "Large File Finder";
    public ScanType ScanType => ScanType.Deep;

    private static readonly HashSet<string> LargeFileSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$RECYCLE.BIN", "System Volume Information",
        "AppData", "node_modules", ".git", ".next", ".gradle",
        "__pycache__", "vendor", ".terraform", "obj", "bin",
        ".vs", ".idea"
    };

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new ConcurrentBag<ScanItem>();
        var driveList = drives.ToList();
        const long minSize = 100 * 1024 * 1024;
        int doneCount = 0;

        await Parallel.ForEachAsync(driveList, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct },
            async (drive, token) =>
        {
            if (!Directory.Exists(drive)) return;
            progress?.Report(($"Scanning {drive} for large files...", 0));
            await Task.Run(() => FindLargeFiles(drive, minSize, items, token), token);
            Interlocked.Increment(ref doneCount);
            progress?.Report(($"Done scanning {drive}", doneCount * 100 / driveList.Count));
        });

        var sorted = items.OrderByDescending(i => i.SizeBytes).ToList();
        progress?.Report(($"Found {sorted.Count} large files.", 100));
        return sorted;
    }

    private static readonly EnumerationOptions EnumOpts = new()
    {
        IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 6
    };

    private void FindLargeFiles(string root, long minSize,
        ConcurrentBag<ScanItem> items, CancellationToken ct)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(root, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false }))
            {
                if (ct.IsCancellationRequested) return;
                var dirName = Path.GetFileName(dir);
                if (LargeFileSkipDirs.Contains(dirName)) continue;
                try { ScanDirForLargeFiles(dir, minSize, items, ct); } catch { }
            }
            // Also scan root for large files
            ScanDirForLargeFiles(root, minSize, items, ct);
        }
        catch { }
    }

    private static void ScanDirForLargeFiles(string dir, long minSize,
        ConcurrentBag<ScanItem> items, CancellationToken ct)
    {
        foreach (var file in Directory.GetFiles(dir, "*.*", EnumOpts))
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var info = new FileInfo(file);
                if (info.Length < minSize) continue;
                var ext = info.Extension.ToLowerInvariant();
                items.Add(new ScanItem
                {
                    Path = file, Name = info.Name, SizeBytes = info.Length,
                    Category = ItemCategory.LargeFile, Extension = ext,
                    LastModified = info.LastWriteTime,
                    Suggestion = GetLargeFileSuggestion(ext, info.Length, info.LastWriteTime)
                });
            }
            catch { }
        }
    }

    private static string GetLargeFileSuggestion(string ext, long size, DateTime lastModified)
    {
        var age = (DateTime.Now - lastModified).TotalDays;
        return ext switch
        {
            ".mp4" or ".mkv" or ".avi" or ".mov" => "Video - consider compressing or moving to external storage",
            ".iso" => "Disk image - delete if no longer needed",
            ".zip" or ".rar" or ".7z" => age > 90
                ? "Old archive - extract if needed, then delete"
                : "Archive - review before deleting",
            ".bak" or ".old" => "Backup file - safe to delete if you have a newer copy",
            ".msi" => "Installer - delete if application is already installed",
            _ => age > 180
                ? $"Old file ({ScanItem.FormatSize(size)}) - review before deleting"
                : $"Large file ({ScanItem.FormatSize(size)}) - review before deleting"
        };
    }
}

/// <summary>
/// Orphan Detector - phát hiện file/thư mục còn sót sau gỡ cài đặt
/// </summary>
public class OrphanDetector(IRuleEngine ruleEngine, IRiskEngine riskEngine) : IScanner
{
    public string Name => "Orphan Detector";
    public ScanType ScanType => ScanType.Deep;

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new List<ScanItem>();
        var installedApps = GetInstalledAppPaths();

        var searchPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        }.Where(Directory.Exists);

        foreach (var basePath in searchPaths)
        {
            if (ct.IsCancellationRequested) break;
            progress?.Report(($"Scanning {basePath} for orphans...", 0));

            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    if (ct.IsCancellationRequested) break;
                    var dirName = Path.GetFileName(dir);
                    if (IsOrphan(dir, installedApps))
                    {
                        var size = GetDirectorySize(dir);
                        if (size > 10 * 1024 * 1024) // Only > 10MB
                        {
                            items.Add(new ScanItem
                            {
                                Path = dir,
                                Name = dirName,
                                SizeBytes = size,
                                IsDirectory = true,
                                Category = ItemCategory.OrphanFile,
                                Risk = RiskLevel.High,
                                RecommendedAction = ItemAction.Quarantine,
                                Suggestion = $"Possible leftover from uninstalled app: {dirName}",
                                AppOrigin = dirName
                            });
                        }
                    }
                }
                ;
            }
            catch { /* skip */ }
        }

        progress?.Report(($"Found {items.Count} orphan items.", 100));
        return items;
    }

    private HashSet<string> GetInstalledAppPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check common uninstall registry keys
        var keys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var keyPath in keys)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var location = subKey?.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(location) && Directory.Exists(location))
                            paths.Add(location.TrimEnd('\\'));
                    }
                    catch { /* skip */ }
                }
            }
            catch { /* skip */ }
        }
        return paths;
    }

    private static bool IsOrphan(string dirPath, HashSet<string> installedPaths)
    {
        var normalized = dirPath.TrimEnd('\\');
        // Check if it matches any installed app path
        if (installedPaths.Contains(normalized)) return false;
        // Check if it's a subdirectory of an installed app
        if (installedPaths.Any(p => normalized.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase)))
            return false;
        // Check if an installed app is inside this directory
        if (installedPaths.Any(p => p.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase)))
            return false;
        return true;
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path, "*.*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                try { size += new FileInfo(file).Length; } catch { /* skip */ }
        }
        catch { /* skip */ }
        return size;
    }
}

/// <summary>
/// Duplicate Finder - tìm file trùng lặp theo kích thước + hash
/// </summary>
public class DuplicateFinder(IRuleEngine ruleEngine, IRiskEngine riskEngine) : IScanner
{
    public string Name => "Duplicate Finder";
    public ScanType ScanType => ScanType.Deep;

    private static readonly HashSet<string> DupSkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$RECYCLE.BIN", "System Volume Information",
        "AppData", "node_modules", ".git", ".next", ".gradle",
        "__pycache__", "obj", "bin", ".vs", ".idea"
    };

    public async Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives, IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var items = new List<ScanItem>();
        var sizeGroups = new Dictionary<long, List<string>>();

        // Phase 1: Group by size — parallel, skip system dirs
        var driveList = drives.ToList();
        int doneCount = 0;

        await Parallel.ForEachAsync(driveList, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = ct },
            async (drive, token) =>
        {
            if (!Directory.Exists(drive)) return;
            progress?.Report(($"Phase 1: Scanning {drive}...", 30));

            await Task.Run(() =>
            {
                try
                {
                    // Scan top-level dirs, skip system
                    foreach (var dir in Directory.GetDirectories(drive, "*", new EnumerationOptions { IgnoreInaccessible = true }))
                    {
                        if (token.IsCancellationRequested) return;
                        var dirName = Path.GetFileName(dir);
                        if (DupSkipDirs.Contains(dirName)) continue;
                        ScanDirForDuplicates(dir, sizeGroups, token);
                    }
                    // Also scan root
                    ScanDirForDuplicates(drive, sizeGroups, token);
                }
                catch { }
            }, token);

            Interlocked.Increment(ref doneCount);
        });

        // Phase 2: Hash potential duplicates
        progress?.Report(("Phase 2: Hashing...", 60));
        var duplicates = sizeGroups.Where(g => g.Value.Count > 1).ToList();
        int processed = 0;

        foreach (var group in duplicates)
        {
            if (ct.IsCancellationRequested) break;
            var hashGroups = new Dictionary<string, List<string>>();

            foreach (var file in group.Value.Take(50))
            {
                try
                {
                    var hash = QuickHash(file);
                    if (!hashGroups.ContainsKey(hash)) hashGroups[hash] = [];
                    hashGroups[hash].Add(file);
                }
                catch { }
            }

            foreach (var hashGroup in hashGroups.Where(h => h.Value.Count > 1))
            {
                var keep = hashGroup.Value[0];
                foreach (var dup in hashGroup.Value.Skip(1))
                {
                    items.Add(new ScanItem
                    {
                        Path = dup, Name = Path.GetFileName(dup), SizeBytes = group.Key,
                        IsDirectory = false, Category = ItemCategory.DuplicateFile,
                        Risk = RiskLevel.Low, RecommendedAction = ItemAction.WarnDelete,
                        Suggestion = $"Duplicate of {Path.GetFileName(keep)}", Hash = hashGroup.Key
                    });
                }
            }

            processed++;
            if (processed % 10 == 0)
                progress?.Report(($"Hashing: {processed}/{duplicates.Count}", 60 + processed * 40 / duplicates.Count));
        }

        progress?.Report(($"Found {items.Count} duplicate files.", 100));
        return items;
    }

    private static void ScanDirForDuplicates(string dir, Dictionary<long, List<string>> sizeGroups,
        CancellationToken ct)
    {
        try
        {
            foreach (var file in Directory.GetFiles(dir, "*.*", new EnumerationOptions
            {
                IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 5
            }))
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var size = new FileInfo(file).Length;
                    if (size < 1024 * 1024) continue;
                    lock (sizeGroups)
                    {
                        if (!sizeGroups.ContainsKey(size)) sizeGroups[size] = [];
                        sizeGroups[size].Add(file);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static string QuickHash(string filePath)
    {
        // Quick partial hash: first + last 4KB
        using var fs = File.OpenRead(filePath);
        var buffer = new byte[8192];
        int read = fs.Read(buffer, 0, 4096);
        if (fs.Length > 4096)
        {
            fs.Seek(-4096, SeekOrigin.End);
            read += fs.Read(buffer, read, 4096);
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(buffer.AsSpan(0, read)));
    }
}
