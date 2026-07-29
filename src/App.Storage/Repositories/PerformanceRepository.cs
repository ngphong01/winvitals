using App.Core;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace App.Storage.Repositories;

/// <summary>
/// Concrete implementation of IPerformanceRepository using LiteDB
/// </summary>
public class PerformanceRepository : IPerformanceRepository
{
    private readonly LiteDatabaseProvider _database;
    private readonly ILogger<PerformanceRepository> _logger;

    public PerformanceRepository(LiteDatabaseProvider database, ILogger<PerformanceRepository> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<PerformanceSnapshot> CreateAsync(PerformanceSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        var db = _database.Instance;
        var col = db.GetCollection<PerformanceSnapshot>("performance");
        col.Insert(snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<PerformanceSnapshot?> GetLatestAsync()
    {
        var db = _database.Instance;
        var result = db.GetCollection<PerformanceSnapshot>("performance")
            .Query().OrderByDescending(x => x.Timestamp).Limit(1).FirstOrDefault();
        return Task.FromResult(result)!;
    }

    public Task<List<PerformanceSnapshot>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var db = _database.Instance;
        var results = db.GetCollection<PerformanceSnapshot>("performance")
            .Query().Where(x => x.Timestamp >= startDate && x.Timestamp <= endDate)
            .OrderBy(x => x.Timestamp).ToList();
        return Task.FromResult(results);
    }

    public Task<List<PerformanceSnapshot>> GetRecentAsync(int minutes = 60)
        => GetByDateRangeAsync(DateTime.Now.AddMinutes(-minutes), DateTime.Now);

    public Task<List<PerformanceSnapshot>> GetByDriveAsync(string driveLetter, int days = 7)
    {
        var db = _database.Instance;
        var results = db.GetCollection<PerformanceSnapshot>("performance")
            .Query().Where(x => x.Timestamp >= DateTime.Now.AddDays(-days) && x.DriveLetter == driveLetter)
            .OrderBy(x => x.Timestamp).ToList();
        return Task.FromResult(results);
    }

    public Task<AppStatistics> GetStatisticsAsync()
    {
        var db = _database.Instance;
        var stats = new AppStatistics();

        var all = db.GetCollection<PerformanceSnapshot>("performance").FindAll().ToList();
        if (all.Count > 0)
        {
            stats.TotalSnapshots = all.Count;
            stats.AvgCpuPercent = Math.Round(all.Average(x => x.CpuPercent), 1);
            stats.AvgMemoryPercent = Math.Round(all.Average(x => x.MemoryPercent), 1);
            stats.AvgDiskPercent = Math.Round(all.Average(x => x.DiskPercent), 1);
            var latest = all.MaxBy(x => x.Timestamp);
            if (latest != null)
            {
                stats.CurrentCpuPercent = latest.CpuPercent;
                stats.CurrentMemoryPercent = latest.MemoryPercent;
                stats.CurrentDiskPercent = latest.DiskPercent;
            }
        }

        var cleanAll = db.GetCollection<CleanHistory>("clean_history").FindAll().ToList();
        if (cleanAll.Count > 0)
        {
            stats.TotalCleanOperations = cleanAll.Count;
            stats.TotalBytesFreed = cleanAll.Sum(x => x.SpaceFreedBytes);
            stats.LastCleanDate = cleanAll.Max(x => x.CleanDate);
        }

        var activeQ = db.GetCollection<QuarantineItem>("quarantine")
            .Query().Where(x => x.Status == QuarantineStatus.Active).ToList();
        stats.ActiveQuarantineItems = activeQ.Count;
        stats.QuarantinedTotalSize = activeQ.Sum(x => x.SizeBytes);

        return Task.FromResult(stats);
    }
}
