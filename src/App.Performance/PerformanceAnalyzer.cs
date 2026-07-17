using System.Diagnostics;
using App.Core;

namespace App.Performance;

/// <summary>
/// Performance Analyzer - CPU, RAM, Disk, process monitoring
/// </summary>
public class PerformanceAnalyzer : IPerformanceAnalyzer
{
    public async Task<PerformanceSnapshot> GetSnapshotAsync()
    {
        var snap = new PerformanceSnapshot { Timestamp = DateTime.Now };

        await Task.Run(() =>
        {
            // CPU — wrapped in try/catch for systems without perf counters
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                Thread.Sleep(100); // Reduced from 500ms — still accurate enough
                snap.CpuPercent = Math.Round(cpuCounter.NextValue(), 1);
            }
            catch { snap.CpuPercent = 0; }
            snap.CpuCoreCount = Environment.ProcessorCount;

            // Memory — use kernel32 GlobalMemoryStatusEx for accurate system RAM
            try
            {
                var memStatus = GetGlobalMemoryStatus();
                if (memStatus.TotalPhys > 0)
                {
                    snap.MemoryTotalGB = Math.Round(memStatus.TotalPhys / (1024.0 * 1024 * 1024), 1);
                    snap.MemoryUsedGB = Math.Round((memStatus.TotalPhys - memStatus.AvailPhys) / (1024.0 * 1024 * 1024), 1);
                    snap.MemoryPercent = Math.Round((1 - memStatus.AvailPhys / (double)memStatus.TotalPhys) * 100, 1);
                }
                else
                {
                    // P/Invoke returned zero — fallback to PerformanceCounter
                    using var memCounter = new PerformanceCounter("Memory", "Available MBytes");
                    var availMB = memCounter.NextValue();
                    snap.MemoryTotalGB = 16; // conservative fallback
                    snap.MemoryUsedGB = Math.Round(snap.MemoryTotalGB - availMB / 1024, 1);
                    snap.MemoryPercent = Math.Round((snap.MemoryUsedGB / snap.MemoryTotalGB) * 100, 1);
                }
            }
            catch
            {
                snap.MemoryTotalGB = 16;
                snap.MemoryUsedGB = 8;
                snap.MemoryPercent = 50;
            }

            // Disk (C: drive)
            try
            {
                var drive = new System.IO.DriveInfo("C:\\");
                if (drive.IsReady)
                {
                    snap.DriveLetter = "C";
                    snap.DiskTotalGB = Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 1);
                    snap.DiskFreeGB = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 1);
                    snap.DiskPercent = Math.Round((1 - drive.AvailableFreeSpace / (double)drive.TotalSize) * 100, 1);
                }
            }
            catch { snap.DriveLetter = "?"; }

            // Top processes
            var processes = Process.GetProcesses()
                .Where(p => { try { _ = p.TotalProcessorTime; return true; } catch { return false; } })
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } })
                .Take(20);

            snap.TopProcesses = processes.Select(p =>
            {
                try
                {
                    return new ProcessInfo
                    {
                        Pid = p.Id,
                        Name = p.ProcessName,
                        MemoryMB = Math.Round(p.WorkingSet64 / (1024.0 * 1024), 1),
                        Status = p.Responding ? "Running" : "Not Responding",
                        IsSystemProcess = IsSystemProcess(p)
                    };
                }
                catch { return null!; }
            }).Where(p => p != null).ToList();

            // Health score
            snap.HealthScore = CalculateHealthScore(snap);

            // Recommendations
            snap.Recommendations = GenerateRecommendations(snap);
        });

        return snap;
    }

    public async Task<List<ProcessInfo>> GetTopProcessesAsync(int topN = 20)
    {
        return await Task.Run(() =>
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(200);
            _ = cpuCounter.NextValue();

            return Process.GetProcesses()
                .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } })
                .Take(topN)
                .Select(p =>
                {
                    try
                    {
                        return new ProcessInfo
                        {
                            Pid = p.Id,
                            Name = p.ProcessName,
                            MemoryMB = Math.Round(p.WorkingSet64 / (1024.0 * 1024), 1),
                            Status = p.Responding ? "Running" : "Not Responding",
                            Publisher = GetPublisher(p),
                            IsSystemProcess = IsSystemProcess(p)
                        };
                    }
                    catch { return null!; }
                })
                .Where(p => p != null)
                .ToList();
        });
    }

    public async Task<List<StartupEntry>> GetStartupEntriesAsync()
    {
        return await Task.Run(() =>
        {
            var entries = new List<StartupEntry>();

            // Registry startup locations
            var paths = new (Microsoft.Win32.RegistryKey Hive, string Path)[]
            {
                (Microsoft.Win32.Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
                (Microsoft.Win32.Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
                (Microsoft.Win32.Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
            };

            foreach (var (hive, path) in paths)
            {
                try
                {
                    using var key = hive.OpenSubKey(path);
                    if (key == null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var command = key.GetValue(name)?.ToString() ?? "";
                        entries.Add(new StartupEntry
                        {
                            Name = name,
                            Command = command,
                            Location = $"Registry: {path}",
                            Enabled = true,
                            Publisher = GuessPublisher(command),
                            Impact = GuessImpact(name, command)
                        });
                    }
                }
                catch { /* skip */ }
            }

            // Startup folder
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder))
            {
                foreach (var file in Directory.GetFiles(startupFolder, "*.lnk"))
                {
                    entries.Add(new StartupEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Command = file,
                        Location = "Startup Folder",
                        Enabled = true,
                        Publisher = "Shortcut",
                        Impact = "Medium"
                    });
                }
            }

            return entries;
        });
    }

    public Task<bool> DisableStartupEntryAsync(StartupEntry entry)
    {
        return Task.Run(() =>
        {
            try
            {
                if (entry.Location.StartsWith("Registry: "))
                {
                    var regPath = entry.Location.Replace("Registry: ", "");
                    var (hive, subPath) = ParseRegistryPath(regPath);
                    using var key = hive.OpenSubKey(subPath, true);
                    key?.DeleteValue(entry.Name, false);
                }
                else if (entry.Location == "Startup Folder")
                {
                    var file = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                        entry.Name + ".lnk");
                    if (File.Exists(file)) File.Delete(file);
                }
                return true;
            }
            catch { return false; }
        });
    }

    public Task<bool> EnableStartupEntryAsync(StartupEntry entry)
    {
        // Re-enable is complex - would need to store backups
        return Task.FromResult(false);
    }

    public async Task<bool> KillProcessAsync(int pid)
    {
        return await Task.Run(() =>
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(5000);
                return true;
            }
            catch { return false; }
        });
    }

    public double CalculateHealthScore(PerformanceSnapshot snap)
    {
        double cpuScore = Math.Max(0, 100 - snap.CpuPercent * 2);
        double memScore = Math.Max(0, 100 - snap.MemoryPercent);
        double diskScore = Math.Max(0, 100 - snap.DiskPercent);
        double procScore = Math.Max(0, 100 - Math.Max(0, snap.TopProcesses.Count - 50) * 0.5);

        return Math.Round((cpuScore + memScore + diskScore + procScore) / 4, 1);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var snap = await GetSnapshotAsync();
        var drives = System.IO.DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
            .Select(d => new App.Core.DriveInfo
            {
                Letter = d.Name.TrimEnd('\\'),
                Label = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                TotalBytes = d.TotalSize,
                FreeBytes = d.AvailableFreeSpace,
                UsedBytes = d.TotalSize - d.AvailableFreeSpace,
                PercentUsed = Math.Round((1 - d.AvailableFreeSpace / (double)d.TotalSize) * 100, 1)
            }).ToList();

        return new DashboardSummary
        {
            Drives = drives,
            EstimatedCleanableBytes = 0, // Will be filled by scan
            HealthScore = snap.HealthScore,
            TopIssues = snap.Recommendations,
            QuarantineCount = 0, // Will be filled
            QuarantineSize = 0
        };
    }

    private static List<string> GenerateRecommendations(PerformanceSnapshot snap)
    {
        var recs = new List<string>();

        if (snap.CpuPercent > 80)
            recs.Add($"⚠️ CPU usage is high ({snap.CpuPercent:F0}%). Check resource-heavy processes.");
        if (snap.MemoryPercent > 85)
            recs.Add($"⚠️ RAM almost full ({snap.MemoryPercent:F0}%). Close unused applications.");
        if (snap.DiskPercent > 90)
            recs.Add($"⚠️ {snap.DriveLetter}: drive almost full ({snap.DiskPercent:F0}%). Run Disk Cleaner.");
        if (snap.MemoryPercent > 70)
            recs.Add($"💡 Memory usage is {snap.MemoryPercent:F0}%. Consider closing background apps.");
        if (snap.DiskPercent > 75)
            recs.Add($"💡 {snap.DriveLetter}: drive is {snap.DiskPercent:F0}% full. Free up some space.");

        var heavyProcs = snap.TopProcesses.Where(p => p.MemoryMB > 1000).Take(3);
        foreach (var p in heavyProcs)
            recs.Add($"🔴 {p.Name} using {p.MemoryMB:F0} MB RAM");

        if (recs.Count == 0)
            recs.Add("✅ System running normally.");

        return recs;
    }

    private static bool IsSystemProcess(Process p)
    {
        try
        {
            var path = p.MainModule?.FileName ?? "";
            return path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string GetPublisher(Process p)
    {
        try { return p.MainModule?.FileVersionInfo.CompanyName ?? ""; }
        catch { return ""; }
    }

    private static string GuessPublisher(string command) => command.ToLowerInvariant() switch
    {
        var c when c.Contains("microsoft") => "Microsoft",
        var c when c.Contains("google") => "Google",
        var c when c.Contains("adobe") => "Adobe",
        var c when c.Contains("nvidia") => "NVIDIA",
        var c when c.Contains("intel") => "Intel",
        _ => "Unknown"
    };

    private static string GuessImpact(string name, string command) => name.ToLowerInvariant() switch
    {
        var n when n.Contains("onedrive") || n.Contains("teams") || n.Contains("chrome") => "High",
        var n when n.Contains("security") || n.Contains("windows") => "Low",
        _ => "Medium"
    };

    private static (Microsoft.Win32.RegistryKey Hive, string SubPath) ParseRegistryPath(string path)
    {
        if (path.StartsWith("HKEY_CURRENT_USER\\"))
            return (Microsoft.Win32.Registry.CurrentUser, path["HKEY_CURRENT_USER\\".Length..]);
        if (path.StartsWith("HKEY_LOCAL_MACHINE\\"))
            return (Microsoft.Win32.Registry.LocalMachine, path["HKEY_LOCAL_MACHINE\\".Length..]);
        return (Microsoft.Win32.Registry.CurrentUser, path);
    }

    // P/Invoke for correct system RAM detection
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private static MEMORYSTATUSEX GetGlobalMemoryStatus()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref memStatus))
            return default; // All zeros — caller must check TotalPhys > 0
        return memStatus;
    }
}
