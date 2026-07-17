namespace App.Core;

/// <summary>
/// Kết quả scan 1 file/thư mục
/// </summary>
public class ScanItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public ItemCategory Category { get; set; } = ItemCategory.Unknown;
    public RiskLevel Risk { get; set; } = RiskLevel.Unknown;
    public ItemAction RecommendedAction { get; set; } = ItemAction.WarnDelete;
    public string Suggestion { get; set; } = string.Empty;
    public string MatchedRule { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AppOrigin { get; set; }  // App nào tạo ra (orphan detection)
    public string? Hash { get; set; }       // SHA256 (duplicate detection)

    public string SizeFormatted => FormatSize(SizeBytes);

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F1} {units[unitIndex]}";
    }
}

/// <summary>
/// Kết quả 1 phiên scan
/// </summary>
public class ScanSession
{
    public int Id { get; set; }
    public ScanType ScanType { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public long TotalItemsFound { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted => ScanItem.FormatSize(TotalSizeBytes);
    public List<string> DrivesScanned { get; set; } = [];
}

/// <summary>
/// Item đã bị cách ly
/// </summary>
public class QuarantineItem
{
    public int Id { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
    public string QuarantinePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted => ScanItem.FormatSize(SizeBytes);
    public DateTime QuarantineDate { get; set; } = DateTime.Now;
    public DateTime? RestoreDate { get; set; }
    public DateTime ExpiryDate { get; set; } = DateTime.Now.AddDays(14);
    public QuarantineStatus Status { get; set; } = QuarantineStatus.Active;
    public string Reason { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public RiskLevel Risk { get; set; }

    public bool IsExpired => DateTime.Now > ExpiryDate;
    public int DaysRemaining => Math.Max(0, (int)(ExpiryDate - DateTime.Now).TotalDays);
}

/// <summary>
/// Snapshot hiệu năng hệ thống
/// </summary>
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double CpuPercent { get; set; }
    public int CpuCoreCount { get; set; }
    public double MemoryTotalGB { get; set; }
    public double MemoryUsedGB { get; set; }
    public double MemoryPercent { get; set; }
    public double DiskTotalGB { get; set; }
    public double DiskFreeGB { get; set; }
    public double DiskPercent { get; set; }
    public string DriveLetter { get; set; } = "C";
    public List<ProcessInfo> TopProcesses { get; set; } = [];
    public double HealthScore { get; set; }
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Thông tin 1 tiến trình
/// </summary>
public class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMB { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public bool IsSystemProcess { get; set; }
    public string Impact => MemoryMB switch
    {
        > 1000 => "High",
        > 200 => "Medium",
        _ => "Low"
    };
}

/// <summary>
/// Startup entry
/// </summary>
public class StartupEntry
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string Impact { get; set; } = "Unknown";
    public bool IsSystem { get; set; }
}

/// <summary>
/// Rule trong Rule Engine
/// </summary>
public class Rule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PathPatterns { get; set; } = [];
    public List<string> Extensions { get; set; } = [];
    public long MinSizeBytes { get; set; }
    public int MaxAgeDays { get; set; }
    public ItemAction Action { get; set; } = ItemAction.WarnDelete;
    public RiskLevel Risk { get; set; } = RiskLevel.Medium;
    public int Priority { get; set; } = 50;
    public bool Enabled { get; set; } = true;
    public CleanLevel CleanLevel { get; set; } = CleanLevel.Custom;
}

/// <summary>
/// Lịch sử dọn dẹp
/// </summary>
public class CleanHistory
{
    public int Id { get; set; }
    public DateTime CleanDate { get; set; } = DateTime.Now;
    public CleanLevel CleanLevel { get; set; }
    public int ItemsCleaned { get; set; }
    public long SpaceFreedBytes { get; set; }
    public string SpaceFreedFormatted => ScanItem.FormatSize(SpaceFreedBytes);
    public int ItemsInQuarantine { get; set; }
}

/// <summary>
/// Thống kê tổng hợp
/// </summary>
public class AppStatistics
{
    public int TotalScans { get; set; }
    public int TotalCleans { get; set; }
    public long TotalSpaceFreed { get; set; }
    public string TotalSpaceFreedFormatted => ScanItem.FormatSize(TotalSpaceFreed);
    public int QuarantineItemCount { get; set; }
    public long QuarantineTotalSize { get; set; }
    public string QuarantineSizeFormatted => ScanItem.FormatSize(QuarantineTotalSize);
    public int BlockedItemsCount { get; set; }
    public DateTime? LastScanDate { get; set; }
    public DateTime? LastCleanDate { get; set; }
}

/// <summary>
/// Disk overview cho Dashboard
/// </summary>
public class DriveInfo
{
    public string Letter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double PercentUsed { get; set; }
    public string TotalFormatted => ScanItem.FormatSize(TotalBytes);
    public string FreeFormatted => ScanItem.FormatSize(FreeBytes);
    public string UsedFormatted => ScanItem.FormatSize(UsedBytes);
}

/// <summary>
/// Summary cho Dashboard
/// </summary>
public class DashboardSummary
{
    public List<DriveInfo> Drives { get; set; } = [];
    public long EstimatedCleanableBytes { get; set; }
    public string EstimatedCleanableFormatted => ScanItem.FormatSize(EstimatedCleanableBytes);
    public double HealthScore { get; set; }
    public string HealthStatus => HealthScore switch
    {
        >= 80 => "Good",
        >= 60 => "Fair",
        _ => "Poor"
    };
    public List<string> TopIssues { get; set; } = [];
    public int QuarantineCount { get; set; }
    public long QuarantineSize { get; set; }
    public string QuarantineSizeFormatted => ScanItem.FormatSize(QuarantineSize);
    public DateTime? LastScanDate { get; set; }
    public DateTime? LastCleanDate { get; set; }
}
