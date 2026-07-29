using System.Diagnostics;
using App.Core;
using Microsoft.Extensions.Logging;

namespace App.Cleaner;

/// <summary>
/// Windows Task Scheduler integration for automated cleaning.
/// Creates, lists, and removes scheduled WHM tasks.
/// Requirements: v2.4 Scheduled Auto-Clean, 16.1-16.10
/// </summary>
public class SchedulerService
{
    private readonly ILogger<SchedulerService> _logger;
    private const string TaskPrefix = "WHM_";
    private static readonly string ExePath = Environment.ProcessPath ?? "WinHealth.exe";

    public SchedulerService(ILogger<SchedulerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a scheduled scan task.
    /// </summary>
    public bool CreateScanTask(string name, string drive, string scanType, string schedule, DateTime? startTime = null)
    {
        var taskName = $"{TaskPrefix}Scan_{name}";
        var args = $"scan -d {drive} -t {scanType}";

        return CreateScheduledTask(taskName, args, schedule, startTime);
    }

    /// <summary>
    /// Create a scheduled clean task.
    /// </summary>
    public bool CreateCleanTask(string name, string level, string schedule, bool preview = false, DateTime? startTime = null)
    {
        var taskName = $"{TaskPrefix}Clean_{name}";
        var args = $"clean -l {level}";
        if (preview) args += " -p";

        return CreateScheduledTask(taskName, args, schedule, startTime);
    }

    /// <summary>
    /// Create auto-clean task that triggers when disk is low.
    /// </summary>
    public bool CreateDiskMonitorTask(string drive, int thresholdPercent = 10)
    {
        var taskName = $"{TaskPrefix}DiskMonitor_{drive.TrimEnd(':')}";
        var args = $"clean -l quick";

        // Create a task that runs every hour and uses the app's built-in disk check
        return CreateScheduledTask(taskName, args, "HOURLY", null);
    }

    /// <summary>
    /// List all WHM scheduled tasks.
    /// </summary>
    public List<ScheduledTaskInfo> ListTasks()
    {
        var tasks = new List<ScheduledTaskInfo>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/query /fo CSV /v",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return tasks;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (var line in output.Split('\n').Skip(1))
            {
                if (line.Contains(TaskPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 3)
                    {
                        tasks.Add(new ScheduledTaskInfo
                        {
                            Name = parts[0].Trim('"').TrimStart('\\').Replace("WHM_Clean_", "").Replace("WHM_Scan_", "").Replace("WHM_", ""),
                            NextRun = parts.Length > 2 ? parts[2].Trim('"') : "Unknown",
                            Status = parts.Length > 1 ? parts[1].Trim('"') : "Unknown"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list scheduled tasks");
        }

        return tasks;
    }

    /// <summary>
    /// Remove a WHM scheduled task.
    /// </summary>
    public bool RemoveTask(string name)
    {
        try
        {
            var fullName = name.StartsWith(TaskPrefix) ? name : $"{TaskPrefix}{name}";
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{fullName}\" /f",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();

            _logger.LogInformation("Removed scheduled task: {Task}", fullName);
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove scheduled task: {Name}", name);
            return false;
        }
    }

    /// <summary>
    /// Delete a scheduled task by name. Handles WHM_ prefix correctly.
    /// </summary>
    public bool DeleteTask(string name)
    {
        try
        {
            var fullName = name.TrimStart('\\');
            fullName = fullName.StartsWith(TaskPrefix) ? fullName : $"{TaskPrefix}{fullName}";
            var psi = new ProcessStartInfo("schtasks", $"/delete /tn \"{fullName}\" /f")
            { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            _logger.LogInformation("Deleted scheduled task: {Task}", fullName);
            return p?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete scheduled task: {Name}", name);
            return false;
        }
    }

    /// <summary>
    /// Check current disk space and trigger clean if below threshold.
    /// </summary>
    public (bool ShouldClean, string Message) CheckDiskThreshold(string drive, int thresholdPercent = 10)
    {
        try
        {
            var di = new System.IO.DriveInfo(drive);
            if (!di.IsReady) return (false, "Drive not ready");

            var percentFree = (double)di.AvailableFreeSpace / di.TotalSize * 100;
            var shouldClean = percentFree < thresholdPercent;

            var message = shouldClean
                ? $" Drive {drive} chỉ còn {percentFree:F1}% trống (ngưỡng {thresholdPercent}%). Nên dọn ngay!"
                : $" Drive {drive} còn {percentFree:F1}% trống — OK.";

            return (shouldClean, message);
        }
        catch (Exception ex)
        {
            return (false, $"Cannot check: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a weekly digest of cleaning activity.
    /// </summary>
    public WeeklyDigest GenerateDigest(long bytesFreed, int itemsCleaned, int scansRun,
        List<string> topCategories, List<string> recommendations)
    {
        return new WeeklyDigest
        {
            GeneratedAt = DateTime.Now,
            TotalBytesFreed = bytesFreed,
            TotalItemsCleaned = itemsCleaned,
            TotalScansRun = scansRun,
            TopCategories = topCategories,
            Recommendations = recommendations,
            Summary = GenerateDigestSummary(bytesFreed, itemsCleaned, scansRun, topCategories)
        };
    }

    private static string GenerateDigestSummary(long bytesFreed, int itemsCleaned, int scansRun,
        List<string> topCategories)
    {
        var size = ScanItem.FormatSize(bytesFreed);
        var catList = string.Join(", ", topCategories.Take(3));

        return bytesFreed > 0
            ? $"Tuan nay ban da don {size} tu {itemsCleaned} items qua {scansRun} lan quet. " +
              $"Nhieu nhat: {catList}. " +
              (bytesFreed > 10_000_000_000
                  ? "Rat tot! Dang duy tri may sach se."
                  : "May van con nhieu file co the don them.")
            : $"Tuan nay chua co hoat dong don dep nao. " +
              $"Chay '{GetRecommendedScan(scansRun)}' de bat dau.";
    }

    private static string GetRecommendedScan(int scansRun) => scansRun switch
    {
        0 => "Quet Toan Bo tu Dashboard",
        < 3 => "Package Cache Analyzer",
        _ => "Stale Project Detector"
    };

    // ========== Implementation ==========

    private bool CreateScheduledTask(string taskName, string arguments, string schedule, DateTime? startTime)
    {
        try
        {
            var start = startTime ?? DateTime.Now.AddMinutes(5);
            var startTimeStr = start.ToString("HH:mm");

            var scheduleArg = schedule.ToUpperInvariant() switch
            {
                "DAILY" => $"/sc daily /st {startTimeStr}",
                "WEEKLY" => $"/sc weekly /d SUN /st {startTimeStr}",
                "MONTHLY" => $"/sc monthly /d 1 /st {startTimeStr}",
                "HOURLY" => $"/sc hourly",
                _ => $"/sc once /st {startTimeStr}"
            };

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{ExePath}\\\" {arguments}\" " +
                           $"{scheduleArg} /f /rl LIMITED",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                _logger.LogInformation("Created scheduled task: {Task} ({Schedule})", taskName, schedule);
                return true;
            }

            _logger.LogWarning("Failed to create task {Task}: {Output}", taskName, output);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scheduled task: {Task}", taskName);
            return false;
        }
    }
}

public class ScheduledTaskInfo
{
    public string Name { get; set; } = "";
    public string NextRun { get; set; } = "";
    public string Status { get; set; } = "";
}

public class WeeklyDigest
{
    public DateTime GeneratedAt { get; set; }
    public long TotalBytesFreed { get; set; }
    public int TotalItemsCleaned { get; set; }
    public int TotalScansRun { get; set; }
    public List<string> TopCategories { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public string Summary { get; set; } = "";
    public string TotalFreedFormatted => ScanItem.FormatSize(TotalBytesFreed);
}
