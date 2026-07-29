using App.Core;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace App.Storage.Repositories;

/// <summary>
/// Concrete implementation of IScanRepository using LiteDB
/// </summary>
public class ScanRepository : IScanRepository
{
    private readonly LiteDatabaseProvider _database;
    private readonly ILogger<ScanRepository> _logger;

    public ScanRepository(LiteDatabaseProvider database, ILogger<ScanRepository> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new scan session
    /// </summary>
    /// <param name="session">The scan session to create</param>
    /// <returns>The created scan session with assigned ID</returns>
    public async Task<ScanSession> CreateAsync(ScanSession session)
    {
        try
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<ScanSessionDoc>("scans");
                var doc = ToDocument(session);
                collection.Insert(doc);
                session.Id = ParseId(doc.Id);
            });

            _logger.LogInformation("Created scan session {SessionId} of type {ScanType}", 
                session.Id, session.ScanType);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scan session of type {ScanType}", session.ScanType);
            throw;
        }
    }

    /// <summary>
    /// Gets a scan session by ID
    /// </summary>
    /// <param name="id">The ID of the scan session</param>
    /// <returns>The scan session if found, null otherwise</returns>
    public async Task<ScanSession?> GetByIdAsync(int id)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<ScanSessionDoc>("scans");
                var objectId = CreateObjectId(id);
                var doc = collection.FindById(objectId);
                
                if (doc == null)
                {
                    _logger.LogDebug("Scan session {SessionId} not found", id);
                    return null;
                }

                return ToEntity(doc);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scan session {SessionId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets scan sessions by type within a date range
    /// </summary>
    /// <param name="type">The scan type to filter by</param>
    /// <param name="days">Number of days to look back (default 30)</param>
    /// <returns>List of matching scan sessions</returns>
    public async Task<List<ScanSession>> GetByTypeAsync(ScanType type, int days = 30)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<ScanSessionDoc>("scans");
                var cutoff = DateTime.Now.AddDays(-Math.Abs(days));
                
                var docs = collection.Find(x => x.ScanType == type.ToString() && x.StartTime >= cutoff)
                    .OrderByDescending(x => x.StartTime)
                    .ToList();

                _logger.LogDebug("Found {Count} scan sessions of type {ScanType} in last {Days} days", 
                    docs.Count, type, days);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scan sessions by type {ScanType}", type);
            throw;
        }
    }

    /// <summary>
    /// Gets recent scan sessions
    /// </summary>
    /// <param name="days">Number of days to look back (default 30)</param>
    /// <returns>List of recent scan sessions</returns>
    public async Task<List<ScanSession>> GetRecentAsync(int days = 30)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<ScanSessionDoc>("scans");
                var cutoff = DateTime.Now.AddDays(-Math.Abs(days));
                
                var docs = collection.Find(x => x.StartTime >= cutoff)
                    .OrderByDescending(x => x.StartTime)
                    .ToList();

                _logger.LogDebug("Found {Count} recent scan sessions in last {Days} days", 
                    docs.Count, days);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent scan sessions");
            throw;
        }
    }

    /// <summary>
    /// Gets the total count of scan sessions
    /// </summary>
    /// <returns>Total number of scan sessions</returns>
    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<ScanSessionDoc>("scans");
                var count = (int)collection.Count();

                _logger.LogDebug("Total scan sessions count: {Count}", count);
                return count;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total scan sessions count");
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Converts a ScanSession entity to a document for storage
    /// </summary>
    private static ScanSessionDoc ToDocument(ScanSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        return new ScanSessionDoc
        {
            ScanType = session.ScanType.ToString(),
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            TotalItems = session.TotalItemsFound,
            TotalSize = session.TotalSizeBytes,
            DrivesScanned = string.Join(",", session.DrivesScanned ?? [])
        };
    }

    /// <summary>
    /// Converts a document to a ScanSession entity
    /// </summary>
    private static ScanSession ToEntity(ScanSessionDoc doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        return new ScanSession
        {
            Id = ParseId(doc.Id),
            ScanType = Enum.Parse<ScanType>(doc.ScanType),
            StartTime = doc.StartTime,
            EndTime = doc.EndTime,
            TotalItemsFound = doc.TotalItems,
            TotalSizeBytes = doc.TotalSize,
            DrivesScanned = doc.DrivesScanned.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
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
    /// Document class for ScanSession storage in LiteDB
    /// </summary>
    private class ScanSessionDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string ScanType { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long TotalItems { get; set; }
        public long TotalSize { get; set; }
        public string DrivesScanned { get; set; } = string.Empty;
    }

    #endregion
}