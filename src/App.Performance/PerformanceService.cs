using App.Core;
using App.Storage.Repositories;
using Microsoft.Extensions.Logging;

namespace App.Performance;

/// <summary>
/// Manages performance metric collection and historical storage.
/// Requirements: 1.3, 3.3, 3.8, 12.4, 12.5
/// </summary>
public class PerformanceService : IPerformanceService
{
    private readonly IPerformanceAnalyzer _analyzer;
    private readonly IPerformanceRepository _repository;
    private readonly ILogger<PerformanceService> _logger;

    // Default alert thresholds
    private const double CpuAlertThreshold = 80.0;
    private const double MemAlertThreshold = 80.0;
    private const double DiskAlertThreshold = 90.0;

    public PerformanceService(
        IPerformanceAnalyzer analyzer,
        IPerformanceRepository repository,
        ILogger<PerformanceService> logger)
    {
        _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PerformanceSnapshot> CaptureSnapshotAsync()
    {
        var snapshot = await _analyzer.GetSnapshotAsync();
        await _repository.CreateAsync(snapshot);
        return snapshot;
    }

    public async Task<List<PerformanceSnapshot>> GetHistoryAsync(TimeSpan timespan)
    {
        var from = DateTime.Now - timespan;
        return await _repository.GetByDateRangeAsync(from, DateTime.Now);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        var summary = await _analyzer.GetDashboardSummaryAsync();
        var stats = await _repository.GetStatisticsAsync();

        // Enrich dashboard with historical data
        if (stats.LastCleanDate.HasValue)
            summary.LastCleanDate = stats.LastCleanDate.Value;

        return summary;
    }

    public List<string> CheckAlerts(PerformanceSnapshot snapshot)
    {
        var alerts = new List<string>();

        if (snapshot.CpuPercent > CpuAlertThreshold)
            alerts.Add($"CPU usage high: {snapshot.CpuPercent:F1}% (threshold: {CpuAlertThreshold}%)");

        if (snapshot.MemoryPercent > MemAlertThreshold)
            alerts.Add($"Memory usage high: {snapshot.MemoryPercent:F1}% (threshold: {MemAlertThreshold}%)");

        if (snapshot.DiskPercent > DiskAlertThreshold)
            alerts.Add($"Disk usage high: {snapshot.DiskPercent:F1}% (threshold: {DiskAlertThreshold}%)");

        if (alerts.Count > 0)
            _logger.LogWarning("Performance alerts: {Alerts}", string.Join("; ", alerts));

        return alerts;
    }
}
