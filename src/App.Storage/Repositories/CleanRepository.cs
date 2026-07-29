using App.Core;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace App.Storage.Repositories;

/// <summary>
/// Concrete implementation of ICleanRepository using LiteDB
/// </summary>
public class CleanRepository : ICleanRepository
{
    private readonly LiteDatabaseProvider _database;
    private readonly ILogger<CleanRepository> _logger;

    public CleanRepository(LiteDatabaseProvider database, ILogger<CleanRepository> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new clean history record
    /// </summary>
    /// <param name="history">The clean history to create</param>
    /// <returns>The created clean history with assigned ID</returns>
    public async Task<CleanHistory> CreateAsync(CleanHistory history)
    {
        try
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                var doc = ToDocument(history);
                collection.Insert(doc);
                history.Id = ParseId(doc.Id);
            });

            _logger.LogInformation("Created clean history record {HistoryId} for level {CleanLevel} with {ItemsCleaned} items", 
                history.Id, history.CleanLevel, history.ItemsCleaned);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create clean history record for level {CleanLevel}", history.CleanLevel);
            throw;
        }
    }

    /// <summary>
    /// Gets a clean history record by ID
    /// </summary>
    /// <param name="id">The ID of the clean history record</param>
    /// <returns>The clean history if found, null otherwise</returns>
    public async Task<CleanHistory?> GetByIdAsync(int id)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                var objectId = CreateObjectId(id);
                var doc = collection.FindById(objectId);
                
                if (doc == null)
                {
                    _logger.LogDebug("Clean history record {HistoryId} not found", id);
                    return null;
                }

                return ToEntity(doc);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get clean history record {HistoryId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets clean history records within a date range
    /// </summary>
    /// <param name="startDate">Start date for the range</param>
    /// <param name="endDate">End date for the range</param>
    /// <returns>List of clean history records in the specified range</returns>
    public async Task<List<CleanHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                
                var docs = collection.Find(x => x.CleanDate >= startDate && x.CleanDate <= endDate)
                    .OrderByDescending(x => x.CleanDate)
                    .ToList();

                _logger.LogDebug("Found {Count} clean history records between {StartDate} and {EndDate}", 
                    docs.Count, startDate.ToShortDateString(), endDate.ToShortDateString());

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get clean history records for date range {StartDate} to {EndDate}", 
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Gets recent clean history records
    /// </summary>
    /// <param name="days">Number of days to look back (default 30)</param>
    /// <returns>List of recent clean history records</returns>
    public async Task<List<CleanHistory>> GetRecentAsync(int days = 30)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                var cutoff = DateTime.Now.AddDays(-Math.Abs(days));
                
                var docs = collection.Find(x => x.CleanDate >= cutoff)
                    .OrderByDescending(x => x.CleanDate)
                    .ToList();

                _logger.LogDebug("Found {Count} recent clean history records in last {Days} days", 
                    docs.Count, days);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent clean history records");
            throw;
        }
    }

    /// <summary>
    /// Gets the total space freed across all clean operations
    /// </summary>
    /// <returns>Total space freed in bytes</returns>
    public async Task<long> GetTotalFreedAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                var totalFreed = collection.FindAll().Sum(x => x.SpaceFreed);

                _logger.LogDebug("Total space freed: {TotalFreed} bytes", totalFreed);
                return totalFreed;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate total freed space");
            throw;
        }
    }

    /// <summary>
    /// Gets the total number of clean operations performed
    /// </summary>
    /// <returns>Total number of clean operations</returns>
    public async Task<int> GetTotalCleansAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<CleanHistoryDoc>("cleanHistory");
                var count = (int)collection.Count();

                _logger.LogDebug("Total clean operations: {Count}", count);
                return count;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total clean operations count");
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Converts a CleanHistory entity to a document for storage
    /// </summary>
    private static CleanHistoryDoc ToDocument(CleanHistory history)
    {
        if (history == null)
            throw new ArgumentNullException(nameof(history));

        return new CleanHistoryDoc
        {
            CleanDate = history.CleanDate,
            CleanLevel = history.CleanLevel.ToString(),
            ItemsCleaned = history.ItemsCleaned,
            SpaceFreed = history.SpaceFreedBytes,
            ItemsInQuarantine = history.ItemsInQuarantine
        };
    }

    /// <summary>
    /// Converts a document to a CleanHistory entity
    /// </summary>
    private static CleanHistory ToEntity(CleanHistoryDoc doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        return new CleanHistory
        {
            Id = ParseId(doc.Id),
            CleanDate = doc.CleanDate,
            CleanLevel = Enum.Parse<CleanLevel>(doc.CleanLevel),
            ItemsCleaned = doc.ItemsCleaned,
            SpaceFreedBytes = doc.SpaceFreed,
            ItemsInQuarantine = doc.ItemsInQuarantine
        };
    }

    /// <summary>
    /// Creates an ObjectId from an integer ID
    /// </summary>
    private static ObjectId CreateObjectId(int id)
    {
        return new ObjectId(id.ToString("X8").PadLeft(24, '0'));
    }

    /// <summary>
    /// Parses an integer ID from an ObjectId
    /// </summary>
    private static int ParseId(ObjectId objectId)
    {
        var hexString = objectId.ToString().Substring(0, Math.Min(8, objectId.ToString().Length));
        return int.Parse(hexString.PadLeft(8, '0'), System.Globalization.NumberStyles.HexNumber);
    }

    #endregion

    #region Document Classes

    /// <summary>
    /// Document class for CleanHistory storage in LiteDB
    /// </summary>
    private class CleanHistoryDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime CleanDate { get; set; }
        public string CleanLevel { get; set; } = string.Empty;
        public int ItemsCleaned { get; set; }
        public long SpaceFreed { get; set; }
        public int ItemsInQuarantine { get; set; }
    }

    #endregion
}