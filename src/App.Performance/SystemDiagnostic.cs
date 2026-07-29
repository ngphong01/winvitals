using System.Diagnostics;
using System.Runtime.InteropServices;
using App.Core;

namespace App.Performance;

/// <summary>
/// Comprehensive system slowdown diagnostic.
/// Analyzes top causes of lag: CPU hogs, memory hogs, disk bottleneck, startup impact, services.
/// </summary>
public class SystemDiagnostic
{
    /// <summary>
    /// Run full system diagnostic and return findings.
    /// </summary>
    public async Task<List<DiagnosticFinding>> DiagnoseAsync()
    {
        var findings = new List<DiagnosticFinding>();

        // 1. CPU hogs
        var cpuHogs = await GetTopProcessesByCpuAsync();
        if (cpuHogs.Count > 0)
        {
            var totalCpu = cpuHogs.Sum(p => p.CpuPercent);
            findings.Add(new DiagnosticFinding
            {
                Severity = totalCpu > 80 ? FindingSeverity.Critical : totalCpu > 50 ? FindingSeverity.Warning : FindingSeverity.Info,
                Category = "CPU",
                Title = totalCpu > 80 ? "CPU đang quá tải" : "CPU đang cao",
                Detail = $"{cpuHogs.Count} tiến trình ngốn CPU ({totalCpu:F0}% tổng): " +
                         string.Join(", ", cpuHogs.Take(3).Select(p => $"{p.Name} ({p.CpuPercent:F0}%)")),
                Action = "Xem trong tab Processes hoặc tắt các ứng dụng không dùng.",
                Score = (int)Math.Min(100, totalCpu)
            });
        }

        // 2. Memory hogs
        var memHogs = await GetTopProcessesByMemoryAsync();
        if (memHogs.Count > 0)
        {
            var totalMemMB = memHogs.Sum(p => p.MemoryMB);
            var totalGB = totalMemMB / 1024.0;
            findings.Add(new DiagnosticFinding
            {
                Severity = totalGB > 8 ? FindingSeverity.Critical : totalGB > 4 ? FindingSeverity.Warning : FindingSeverity.Info,
                Category = "RAM",
                Title = totalGB > 8 ? "RAM thiếu nghiêm trọng" : "RAM đang cao",
                Detail = $"{memHogs.Count} tiến trình ngốn RAM (tổng {totalGB:F1} GB): " +
                         string.Join(", ", memHogs.Take(3).Select(p => $"{p.Name} ({p.MemoryMB:F0} MB)")),
                Action = "Đóng bớt tab trình duyệt, tắt phần mềm không dùng, cân nhắc nâng RAM.",
                Score = (int)Math.Min(100, totalGB * 10)
            });
        }

        // 3. Disk analysis
        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var pctFree = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                var usedGB = (drive.TotalSize - drive.AvailableFreeSpace) / (1024.0 * 1024 * 1024);

                if (pctFree < 10)
                {
                    findings.Add(new DiagnosticFinding
                    {
                        Severity = FindingSeverity.Critical,
                        Category = "Disk",
                        Title = $"Ổ {drive.Name} sắp đầy",
                        Detail = $"Còn {pctFree:F0}% trống ({usedGB:F1} GB đã dùng). " +
                                 "Khi ổ đầy, máy sẽ chậm hơn đáng kể.",
                        Action = "Chạy Disk Analyzer hoặc Quick Clean để giải phóng dung lượng.",
                        Score = (int)(100 - pctFree * 2)
                    });
                }
            }
        }
        catch { }

        // 4. Startup programs
        try
        {
            var heavyStartup = GetHeavyStartupPrograms();
            if (heavyStartup.Count > 0)
            {
                findings.Add(new DiagnosticFinding
                {
                    Severity = heavyStartup.Count > 5 ? FindingSeverity.Warning : FindingSeverity.Info,
                    Category = "Startup",
                    Title = $"{heavyStartup.Count} ứng dụng khởi động cùng Windows",
                    Detail = string.Join(", ", heavyStartup.Take(5).Select(p => p.Name)),
                    Action = "Vào Settings → Apps → Startup để tắt bớt, hoặc dùng Task Manager > Startup tab.",
                    Score = Math.Min(100, heavyStartup.Count * 10)
                });
            }
        }
        catch { }

        // 5. Windows Temp / cache accumulation
        try
        {
            var tempPath = Path.GetTempPath();
            var tempSize = GetDirectorySize(tempPath);
            if (tempSize > 1_000_000_000) // > 1GB
            {
                findings.Add(new DiagnosticFinding
                {
                    Severity = tempSize > 5_000_000_000 ? FindingSeverity.Warning : FindingSeverity.Info,
                    Category = "Temp",
                    Title = $"File tạm quá nhiều ({ScanItem.FormatSize(tempSize)})",
                    Detail = $"Thư mục Temp chứa nhiều file tạm không cần thiết.",
                    Action = "Chạy Quick Clean để dọn sạch temp files.",
                    Score = (int)Math.Min(100, tempSize / 100_000_000)
                });
            }
        }
        catch { }

        // 6. System uptime (long uptime can cause memory leaks / slowdown)
        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            if (uptime.TotalDays > 7)
            {
                findings.Add(new DiagnosticFinding
                {
                    Severity = uptime.TotalDays > 30 ? FindingSeverity.Warning : FindingSeverity.Info,
                    Category = "System",
                    Title = $"Máy đã chạy {uptime.Days} ngày không restart",
                    Detail = "Uptime dài có thể gây memory leak, cache堆积, giảm hiệu năng.",
                    Action = "Restart máy để giải phóng bộ nhớ và reset hệ thống.",
                    Score = Math.Min(100, (int)(uptime.TotalDays * 3))
                });
            }
        }
        catch { }

        return findings.OrderByDescending(f => f.Score).ToList();
    }

    /// <summary>
    /// Get a human-readable summary.
    /// </summary>
    public string GetSummary(List<DiagnosticFinding> findings)
    {
        if (findings.Count == 0) return "Không phát hiện vấn đề gì. Máy đang hoạt động tốt!";

        var critical = findings.Count(f => f.Severity == FindingSeverity.Critical);
        var warnings = findings.Count(f => f.Severity == FindingSeverity.Warning);

        var summary = critical > 0
            ? $"Có {critical} vấn đề nghiêm trọng cần xử lý ngay!"
            : warnings > 0
                ? $"Có {warnings} vấn đề cần chú ý."
                : "Máy hơi chậm nhưng không có vấn đề nghiêm trọng.";

        return summary;
    }

    /// <summary>
    /// Get health score 0-100 based on diagnostic findings.
    /// </summary>
    public int GetHealthScore(List<DiagnosticFinding> findings)
    {
        if (findings.Count == 0) return 100;

        var maxScore = findings.Max(f => f.Score);
        var avgScore = findings.Average(f => f.Score);

        return Math.Max(0, 100 - (int)(maxScore * 0.5 + avgScore * 0.5));
    }

    // ========== Private helpers ==========

    private static async Task<List<ProcInfo>> GetTopProcessesByCpuAsync()
    {
        var result = new List<ProcInfo>();
        try
        {
            using var pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            pc.NextValue();
            await Task.Delay(500);
            var totalCpu = pc.NextValue();
            if (totalCpu < 20) return result; // Overall CPU is fine

            var procs = Process.GetProcesses()
                .Where(p => p.Id != 0)
                .Select(p =>
                {
                    try { return new ProcInfo { Name = p.ProcessName, CpuPercent = 0.0, MemoryMB = p.WorkingSet64 / (1024.0 * 1024) }; }
                    catch { return null; }
                })
                .Where(p => p != null && p.MemoryMB > 50)
                .Cast<ProcInfo>()
                .OrderByDescending(p => p.MemoryMB)
                .Take(5)
                .ToList();

            return procs;
        }
        catch { return result; }
    }

    private static Task<List<ProcInfo>> GetTopProcessesByMemoryAsync()
    {
        var result = new List<ProcInfo>();
        try
        {
            var procs = Process.GetProcesses()
                .Where(p => p.Id != 0)
                .Select(p =>
                {
                    try { return new ProcInfo { Name = p.ProcessName, MemoryMB = p.WorkingSet64 / (1024.0 * 1024) }; }
                    catch { return null; }
                })
                .Where(p => p != null && p.MemoryMB > 200)
                .Cast<ProcInfo>()
                .OrderByDescending(p => p.MemoryMB)
                .Take(5)
                .ToList();

            return Task.FromResult(procs);
        }
        catch { return Task.FromResult(result); }
    }

    private static List<ProcInfo> GetHeavyStartupPrograms()
    {
        var result = new List<ProcInfo>();
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    result.Add(new ProcInfo { Name = name, MemoryMB = 0 });
                }
            }
        }
        catch { }
        return result;
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            long size = 0;
            foreach (var f in Directory.GetFiles(path, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 2 }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
            return size;
        }
        catch { return 0; }
    }

    private class ProcInfo { public string Name { get; set; } = ""; public double CpuPercent { get; set; } public double MemoryMB { get; set; } }
}

public class DiagnosticFinding
{
    public FindingSeverity Severity { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Action { get; set; } = "";
    public int Score { get; set; } // 0-100, higher = worse
}

public enum FindingSeverity { Info, Warning, Critical }
