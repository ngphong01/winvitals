using System.Diagnostics;
using App.Core;
using App.Storage.Repositories;

namespace App.Cleaner;

/// <summary>
/// Weekly report / digest generator.
/// Summarizes cleaning activity, disk health, and recommendations.
/// Requirements: v3.1 Weekly Digest
/// </summary>
public class ReportGenerator
{
    private readonly ICleanRepository _cleanRepo;
    private readonly IPerformanceRepository _perfRepo;
    private readonly IQuarantineRepository _quarantineRepo;

    public ReportGenerator(
        ICleanRepository cleanRepo,
        IPerformanceRepository perfRepo,
        IQuarantineRepository quarantineRepo)
    {
        _cleanRepo = cleanRepo;
        _perfRepo = perfRepo;
        _quarantineRepo = quarantineRepo;
    }

    /// <summary>
    /// Generate a weekly summary report.
    /// </summary>
    public async Task<WeeklyReport> GenerateWeeklyAsync()
    {
        var weekAgo = DateTime.Now.AddDays(-7);
        var cleans = await _cleanRepo.GetByDateRangeAsync(weekAgo, DateTime.Now);
        var perf = await _perfRepo.GetRecentAsync(60);

        long totalFreed = cleans.Sum(c => c.SpaceFreedBytes);
        int totalItems = cleans.Sum(c => c.ItemsCleaned);
        int scanCount = cleans.Count;

        var topCategories = cleans
            .GroupBy(c => c.CleanLevel)
            .OrderByDescending(g => g.Sum(x => x.SpaceFreedBytes))
            .Take(3)
            .Select(g => $"{g.Key}: {ScanItem.FormatSize(g.Sum(x => x.SpaceFreedBytes))}")
            .ToList();

        var oldestSnapshot = perf.Count > 0 ? perf.MinBy(p => p.Timestamp) : null;
        var latestSnapshot = perf.Count > 0 ? perf.MaxBy(p => p.Timestamp) : null;

        // Calculate disk trend
        var diskTrend = latestSnapshot != null && oldestSnapshot != null
            ? Math.Round(latestSnapshot.DiskPercent - oldestSnapshot.DiskPercent, 1)
            : 0;

        var activeQ = await _quarantineRepo.GetActiveAsync();
        var expiredQ = await _quarantineRepo.GetExpiredAsync();

        var report = new WeeklyReport
        {
            GeneratedAt = DateTime.Now,
            PeriodStart = weekAgo,
            PeriodEnd = DateTime.Now,
            TotalBytesFreed = totalFreed,
            TotalItemsCleaned = totalItems,
            TotalScans = scanCount,
            TopCleanedCategories = topCategories,
            DiskUsageTrend = diskTrend,
            DiskTrendDirection = diskTrend switch
            {
                > 2 => "Dung luong tang nhanh, can don ngay!",
                > 0 => "Dung luong tang nhe, theo doi thuong xuyen.",
                _ => "Dung luong on dinh hoac giam."
            },
            ActiveQuarantineCount = activeQ.Count,
            ExpiredQuarantineCount = expiredQ.Count,
            Recommendations = GenerateRecommendations(totalFreed, diskTrend, activeQ.Count, scanCount)
        };

        if (latestSnapshot != null)
        {
            report.CurrentCpuPercent = latestSnapshot.CpuPercent;
            report.CurrentMemoryPercent = latestSnapshot.MemoryPercent;
            report.CurrentDiskPercent = latestSnapshot.DiskPercent;
        }

        return report;
    }

    /// <summary>
    /// Generate a human-readable (Vietnamese) summary string.
    /// </summary>
    public string FormatAsText(WeeklyReport report)
    {
        return $"""
            = Windows Health Manager - Weekly Report =
            Period: {report.PeriodStart:dd/MM/yyyy} - {report.PeriodEnd:dd/MM/yyyy}

            DON DEP:
              Tong dung luong da giai phong: {ScanItem.FormatSize(report.TotalBytesFreed)}
              Tong items da don: {report.TotalItemsCleaned}
              So lan quet: {report.TotalScans}

            HE THONG:
              CPU: {report.CurrentCpuPercent:F0}%
              RAM: {report.CurrentMemoryPercent:F0}%
              Disk: {report.CurrentDiskPercent:F0}%

            CACH LY:
              Active: {report.ActiveQuarantineCount} items
              Het han: {report.ExpiredQuarantineCount} items

            KHUYEN NGHI:
            {string.Join("\n", report.Recommendations.Select(r => $"  - {r}"))}
            """;
    }

    /// <summary>
    /// Generate HTML report for prettier viewing.
    /// </summary>
    public string FormatAsHtml(WeeklyReport report)
    {
        var html = $"<html><head><meta charset=\"utf-8\"><style>\n" +
            "body { font-family: 'Segoe UI', sans-serif; background: #0D0D1A; color: #DDE0F5; padding: 32px; }\n" +
            "h1 { color: #6C8CF0; font-size: 22px; border-bottom: 1px solid #25254A; }\n" +
            ".stat { background: #12122A; border: 1px solid #25254A; border-radius: 8px; padding: 16px; margin: 8px 0; }\n" +
            ".stat .value { font-size: 24px; font-weight: bold; color: #7ECF6A; }\n" +
            ".muted { color: #5A5C80; font-size: 11px; }\n" +
            "</style></head><body>\n" +
            $"<h1>Windows Health Manager - Weekly Report</h1>\n" +
            $"<p class=\"muted\">{report.PeriodStart:dd/MM/yyyy} -> {report.PeriodEnd:dd/MM/yyyy}</p>\n" +
            $"<div class=\"stat\"><h2>Da Giai Phong</h2><div class=\"value\">{ScanItem.FormatSize(report.TotalBytesFreed)}</div>" +
            $"<p>{report.TotalItemsCleaned} items - {report.TotalScans} scans</p></div>\n" +
            $"<div class=\"stat\"><h2>He Thong</h2>" +
            $"<p>CPU {report.CurrentCpuPercent:F0}% - RAM {report.CurrentMemoryPercent:F0}% - Disk {report.CurrentDiskPercent:F0}%</p>" +
            $"<p>{report.DiskTrendDirection}</p></div>\n" +
            $"<div class=\"stat\"><h2>Khuyen Nghi</h2>" +
            $"<ul>{string.Join("", report.Recommendations.Select(r => $"<li>{r}</li>"))}</ul></div>\n" +
            $"<p class=\"muted\">Generated by WHM v2.0 at {report.GeneratedAt:yyyy-MM-dd HH:mm}</p>\n" +
            "</body></html>";
        return html;
    }

    /// <summary>
    /// Save report to file and open it.
    /// </summary>
    public async Task ShowHtmlReportAsync()
    {
        var report = await GenerateWeeklyAsync();
        var html = FormatAsHtml(report);
        var path = Path.Combine(Path.GetTempPath(), $"whm-report-{DateTime.Now:yyyyMMdd}.html");
        await File.WriteAllTextAsync(path, html);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static List<string> GenerateRecommendations(long totalFreed, double diskTrend, int quarantineCount, int scanCount)
    {
        var recs = new List<string>();

        if (totalFreed == 0)
            recs.Add("Chua don lan nao — chay Quick Clean de bat dau.");
        else if (totalFreed < 5_000_000_000)
            recs.Add("Da don duoc {ScanItem.FormatSize(totalFreed)} — con nhieu, chay Package Cache Analyzer de don them.");

        if (diskTrend > 5)
            recs.Add("Disk dung luong dang tang nhanh ({diskTrend:F0}% trong tuan) — can don sau (Deep Monthly Preset).");

        if (quarantineCount > 5)
            recs.Add($"Co {quarantineCount} items trong quarantine — kiem tra va restore/delete.");

        if (scanCount < 2)
            recs.Add("Chay Package Cache Analyzer de giai phong them GB.");

        if (recs.Count == 0)
            recs.Add("May tinh dang o trang thai tot! Tiep tuc duy tri.");

        return recs;
    }
}

public class WeeklyReport
{
    public DateTime GeneratedAt { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public long TotalBytesFreed { get; set; }
    public int TotalItemsCleaned { get; set; }
    public int TotalScans { get; set; }
    public List<string> TopCleanedCategories { get; set; } = [];
    public double DiskUsageTrend { get; set; }
    public string DiskTrendDirection { get; set; } = "";
    public int ActiveQuarantineCount { get; set; }
    public int ExpiredQuarantineCount { get; set; }
    public List<string> Recommendations { get; set; } = [];
    public double CurrentCpuPercent { get; set; }
    public double CurrentMemoryPercent { get; set; }
    public double CurrentDiskPercent { get; set; }
    public string TotalFreedFormatted => ScanItem.FormatSize(TotalBytesFreed);
}
