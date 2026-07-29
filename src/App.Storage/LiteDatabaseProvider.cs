using LiteDB;
using App.Core;
using System.IO;

namespace App.Storage;

/// <summary>
/// LiteDB database provider with proper schema and initialization
/// </summary>
public class LiteDatabaseProvider : IDisposable
{
    private readonly string _connectionString;
    private readonly LiteDatabase _database;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the database path
    /// </summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Gets the LiteDB instance
    /// </summary>
    public ILiteDatabase Instance => _database;

    /// <summary>
    /// Gets or sets the schema version
    /// </summary>
    public int SchemaVersion { get; private set; } = 1;

    /// <summary>
    /// Initializes LiteDB database provider with specified path
    /// Ensures proper directory creation and database initialization
    /// Requirements 2.1, 2.4, 2.8: Database initialization with proper location
    /// </summary>
    /// <param name="dbPath">Full path to the database file</param>
    public LiteDatabaseProvider(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));
        }

        // Ensure directory exists (Requirement 2.1: Database initialization)
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = dbPath;
        
        try
        {
            // Initialize LiteDB with the specified path
            // Requirement 2.4: Configure database file location
            _database = new LiteDatabase(dbPath);
            
            // Initialize schema and collections
            // Requirement 2.1: Database initialization
            InitializeDatabase();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize LiteDB database at path: {dbPath}", ex);
        }
    }
    /// <summary>
    /// Initializes database schema, collections, and indexes
    /// Requirement 2.1: Database initialization
    /// Requirement 2.8: Database connection management
    /// </summary>
    private void InitializeDatabase()
    {
        lock (_lock)
        {
            // Create collections with proper schema
            // Requirement 2.1: Initialize database structures
            var sessions = _database.GetCollection<ScanSessionDoc>("scans");
            var items = _database.GetCollection<ScanItemDoc>("scanItems");
            var history = _database.GetCollection<CleanHistoryDoc>("cleanHistory");
            var quarantine = _database.GetCollection<QuarantineItemDoc>("quarantine");
            var rules = _database.GetCollection<RuleDoc>("rules");
            var performance = _database.GetCollection<PerformanceSnapshotDoc>("performance");
            var settings = _database.GetCollection<SettingDoc>("settings");

            // Create indexes for performance optimization
            CreateIndexes(sessions, items, history, quarantine, rules, performance);

            // Initialize default settings
            EnsureSettings(settings);

            // Load current schema version
            SchemaVersion = GetSchemaVersion(settings);
        }
    }

    private void CreateIndexes(ILiteCollection<ScanSessionDoc> sessions, ILiteCollection<ScanItemDoc> items,
        ILiteCollection<CleanHistoryDoc> history, ILiteCollection<QuarantineItemDoc> quarantine,
        ILiteCollection<RuleDoc> rules, ILiteCollection<PerformanceSnapshotDoc> performance)
    {
        // Scan indexes
        sessions.EnsureIndex(x => x.ScanType);
        sessions.EnsureIndex(x => x.StartTime);
        sessions.EnsureIndex(x => x.EndTime);

        // Scan item indexes
        items.EnsureIndex(x => x.SessionId);
        items.EnsureIndex(x => x.Path);
        items.EnsureIndex(x => x.Risk);
        items.EnsureIndex(x => x.Category);
        items.EnsureIndex(x => x.Extension);
        items.EnsureIndex(x => x.LastModified);

        // Clean history indexes
        history.EnsureIndex(x => x.CleanDate);
        history.EnsureIndex(x => x.CleanLevel);

        // Quarantine indexes
        quarantine.EnsureIndex(x => x.Status);
        quarantine.EnsureIndex(x => x.QuarantineDate);
        quarantine.EnsureIndex(x => x.ExpiryDate);
        quarantine.EnsureIndex(x => x.OriginalPath);

        // Performance indexes
        performance.EnsureIndex(x => x.Timestamp);
        performance.EnsureIndex(x => x.DriveLetter);
    }

    private void EnsureSettings(ILiteCollection<SettingDoc> settings)
    {
        // Ensure schema version setting exists
        var existing = settings.FindById("schema_version");
        if (existing == null)
        {
            settings.Insert(new SettingDoc { Key = "schema_version", Value = "1" });
        }
    }

    private int GetSchemaVersion(ILiteCollection<SettingDoc> settings)
    {
        var doc = settings.FindById("schema_version");
        return doc != null && int.TryParse(doc.Value, out int version) ? version : 1;
    }

    /// <summary>
    /// Save scan session to database
    /// </summary>
    public void SaveScanSession(ScanSession session)
    {
        var collection = _database.GetCollection<ScanSessionDoc>("scans");
        var doc = new ScanSessionDoc
        {
            Id = new ObjectId(session.Id.ToString("X8").PadLeft(24, '0')),
            ScanType = session.ScanType.ToString(),
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            TotalItems = session.TotalItemsFound,
            TotalSize = session.TotalSizeBytes,
            DrivesScanned = string.Join(",", session.DrivesScanned)
        };
        collection.Insert(doc);
    }

    /// <summary>
    /// Get scan history from database
    /// </summary>
    public List<ScanSession> GetScanHistory(int days = 30)
    {
        var collection = _database.GetCollection<ScanSessionDoc>("scans");
        var cutoff = DateTime.Now.AddDays(-days);
        
        var docs = collection.Find(x => x.StartTime >= cutoff).OrderByDescending(x => x.StartTime).ToList();
        
        return docs.Select(ToScanSession).ToList();
    }

    /// <summary>
    /// Save clean history to database
    /// </summary>
    public void SaveCleanHistory(CleanHistory history)
    {
        var collection = _database.GetCollection<CleanHistoryDoc>("cleanHistory");
        var doc = new CleanHistoryDoc
        {
            Id = new ObjectId(history.Id.ToString("X8").PadLeft(24, '0')),
            CleanDate = history.CleanDate,
            CleanLevel = history.CleanLevel.ToString(),
            ItemsCleaned = history.ItemsCleaned,
            SpaceFreed = history.SpaceFreedBytes,
            ItemsInQuarantine = history.ItemsInQuarantine
        };
        collection.Insert(doc);
    }

    /// <summary>
    /// Get clean history from database
    /// </summary>
    public List<CleanHistory> GetCleanHistory(int days = 30)
    {
        var collection = _database.GetCollection<CleanHistoryDoc>("cleanHistory");
        var cutoff = DateTime.Now.AddDays(-days);
        
        var docs = collection.Find(x => x.CleanDate >= cutoff).OrderByDescending(x => x.CleanDate).ToList();
        
        return docs.Select(ToCleanHistory).ToList();
    }

    /// <summary>
    /// Save quarantine item to database
    /// </summary>
    public void SaveQuarantineItem(QuarantineItem item)
    {
        var collection = _database.GetCollection<QuarantineItemDoc>("quarantine");
        var doc = new QuarantineItemDoc
        {
            Id = new ObjectId(item.Id.ToString("X8").PadLeft(24, '0')),
            OriginalPath = item.OriginalPath,
            QuarantinePath = item.QuarantinePath,
            FileName = item.FileName,
            SizeBytes = item.SizeBytes,
            QuarantineDate = item.QuarantineDate,
            RestoreDate = item.RestoreDate,
            ExpiryDate = item.ExpiryDate,
            Status = item.Status.ToString(),
            Reason = item.Reason,
            SourceModule = item.SourceModule,
            Risk = item.Risk.ToString()
        };
        collection.Insert(doc);
    }

    /// <summary>
    /// Get all quarantine items from database
    /// </summary>
    public List<QuarantineItem> GetQuarantineItems()
    {
        var collection = _database.GetCollection<QuarantineItemDoc>("quarantine");
        
        var docs = collection.FindAll().OrderByDescending(x => x.QuarantineDate).ToList();
        
        return docs.Select(ToQuarantineItem).ToList();
    }

    /// <summary>
    /// Remove quarantine item from database
    /// </summary>
    public bool RemoveQuarantineItem(int id)
    {
        var collection = _database.GetCollection<QuarantineItemDoc>("quarantine");
        var objectId = new ObjectId(id.ToString("X8").PadLeft(24, '0'));
        return collection.Delete(objectId);
    }

    /// <summary>
    /// Update quarantine item status
    /// </summary>
    public bool UpdateQuarantineStatus(int id, QuarantineStatus status)
    {
        var collection = _database.GetCollection<QuarantineItemDoc>("quarantine");
        var objectId = new ObjectId(id.ToString("X8").PadLeft(24, '0'));
        var doc = collection.FindById(objectId);
        
        if (doc == null) return false;
        
        doc.Status = status.ToString();
        collection.Update(doc);
        return true;
    }

    /// <summary>
    /// Get setting value from database
    /// </summary>
    public string? GetSetting(string key)
    {
        var collection = _database.GetCollection<SettingDoc>("settings");
        var doc = collection.FindOne(x => x.Key == key);
        return doc?.Value;
    }

    /// <summary>
    /// Set setting value in database
    /// </summary>
    public void SetSetting(string key, string value)
    {
        var collection = _database.GetCollection<SettingDoc>("settings");
        var doc = collection.FindOne(x => x.Key == key);
        
        if (doc != null)
        {
            doc.Value = value;
            collection.Update(doc);
        }
        else
        {
            collection.Insert(new SettingDoc { Key = key, Value = value });
        }
    }

    /// <summary>
    /// Update schema version in database
    /// </summary>
    public void UpdateSchemaVersion(int version)
    {
        var collection = _database.GetCollection<SettingDoc>("settings");
        var doc = collection.FindById("schema_version");
        
        if (doc != null)
        {
            doc.Value = version.ToString();
            collection.Update(doc);
            SchemaVersion = version;
        }
    }

    /// <summary>
    /// Get database statistics
    /// </summary>
    public AppStatistics GetStatistics()
    {
        var sessions = _database.GetCollection<ScanSessionDoc>("scans");
        var history = _database.GetCollection<CleanHistoryDoc>("cleanHistory");
        var quarantine = _database.GetCollection<QuarantineItemDoc>("quarantine");
        var items = _database.GetCollection<ScanItemDoc>("scanItems");

        var stats = new AppStatistics
        {
            TotalScans = (int)sessions.Count(),
            TotalCleans = (int)history.Count(),
            TotalSpaceFreed = history.FindAll().Sum(x => x.SpaceFreed),
            QuarantineItemCount = (int)quarantine.Count(x => x.Status == "Active"),
            QuarantineTotalSize = quarantine.FindAll().Sum(x => x.SizeBytes),
            BlockedItemsCount = (int)items.Count(x => x.Action == "Block"),
            LastScanDate = sessions.FindAll().OrderByDescending(x => x.StartTime).FirstOrDefault()?.StartTime,
            LastCleanDate = history.FindAll().OrderByDescending(x => x.CleanDate).FirstOrDefault()?.CleanDate
        };

        return stats;
    }

    private static ScanSession ToScanSession(ScanSessionDoc doc)
    {
        return new ScanSession
        {
            Id = int.Parse(doc.Id.ToString().Substring(0, Math.Min(8, doc.Id.ToString().Length)).PadLeft(8, '0')),
            ScanType = Enum.Parse<ScanType>(doc.ScanType),
            StartTime = doc.StartTime,
            EndTime = doc.EndTime,
            TotalItemsFound = doc.TotalItems,
            TotalSizeBytes = doc.TotalSize,
            DrivesScanned = doc.DrivesScanned.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
        };
    }

    private static ScanItem ToScanItem(ScanItemDoc doc)
    {
        return new ScanItem
        {
            Id = doc.Id.ToString(),
            Path = doc.Path,
            Name = doc.Name,
            SizeBytes = doc.SizeBytes,
            IsDirectory = doc.IsDirectory,
            Category = Enum.Parse<ItemCategory>(doc.Category),
            Risk = Enum.Parse<RiskLevel>(doc.Risk),
            RecommendedAction = Enum.Parse<ItemAction>(doc.Action),
            Suggestion = doc.Suggestion,
            MatchedRule = doc.MatchedRule,
            Extension = doc.Extension,
            LastModified = doc.LastModified,
            CreatedAt = doc.CreatedAt,
            AppOrigin = doc.AppOrigin,
            Hash = doc.Hash
        };
    }

    private static CleanHistory ToCleanHistory(CleanHistoryDoc doc)
    {
        return new CleanHistory
        {
            Id = TryParseIntId(doc.Id),
            CleanDate = doc.CleanDate,
            CleanLevel = Enum.Parse<CleanLevel>(doc.CleanLevel),
            ItemsCleaned = doc.ItemsCleaned,
            SpaceFreedBytes = doc.SpaceFreed,
            ItemsInQuarantine = doc.ItemsInQuarantine
        };
    }

    private static QuarantineItem ToQuarantineItem(QuarantineItemDoc doc)
    {
        return new QuarantineItem
        {
            Id = int.Parse(doc.Id.ToString().Substring(0, Math.Min(8, doc.Id.ToString().Length)).PadLeft(8, '0')),
            OriginalPath = doc.OriginalPath,
            QuarantinePath = doc.QuarantinePath,
            FileName = doc.FileName,
            SizeBytes = doc.SizeBytes,
            QuarantineDate = doc.QuarantineDate,
            RestoreDate = doc.RestoreDate,
            ExpiryDate = doc.ExpiryDate,
            Status = Enum.Parse<QuarantineStatus>(doc.Status),
            Reason = doc.Reason,
            SourceModule = doc.SourceModule,
            Risk = Enum.Parse<RiskLevel>(doc.Risk)
        };
    }

    #region Document Classes

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

    private class ScanItemDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string SessionId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool IsDirectory { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string MatchedRule { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AppOrigin { get; set; }
        public string? Hash { get; set; }
    }

    private class CleanHistoryDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime CleanDate { get; set; }
        public string CleanLevel { get; set; } = string.Empty;
        public int ItemsCleaned { get; set; }
        public long SpaceFreed { get; set; }
        public int ItemsInQuarantine { get; set; }
    }

    private class QuarantineItemDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string OriginalPath { get; set; } = string.Empty;
        public string QuarantinePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime QuarantineDate { get; set; }
        public DateTime? RestoreDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
    }

    private class RuleDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PathPatterns { get; set; } = string.Empty;
        public string Extensions { get; set; } = string.Empty;
        public long MinSizeBytes { get; set; }
        public int MaxAgeDays { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public int Priority { get; set; } = 50;
        public bool Enabled { get; set; } = true;
        public string CleanLevel { get; set; } = string.Empty;
    }

    private class PerformanceSnapshotDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime Timestamp { get; set; }
        public double CpuPercent { get; set; }
        public double MemoryTotalGB { get; set; }
        public double MemoryUsedGB { get; set; }
        public double MemoryPercent { get; set; }
        public double DiskTotalGB { get; set; }
        public double DiskFreeGB { get; set; }
        public double DiskPercent { get; set; }
        public string DriveLetter { get; set; } = "C";
        public string TopProcesses { get; set; } = string.Empty;
        public double HealthScore { get; set; }
        public string Recommendations { get; set; } = string.Empty;
    }

    private class SettingDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    #endregion

    /// <summary>
    /// Disposes the LiteDB database connection
    /// Requirement 2.8: Database connection properly closed when application shuts down
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }

    /// <summary>
    /// Parse LiteDB BsonValue to int safely — handles ObjectId, string, and numeric IDs.
    /// </summary>
    private static int TryParseIntId(BsonValue id)
    {
        try
        {
            if (id.IsInt32) return id.AsInt32;
            if (id.IsInt64) return (int)id.AsInt64;
            var str = id.ToString();
            if (int.TryParse(str, out var result)) return result;
            return Math.Abs(str.GetHashCode());
        }
        catch { return Math.Abs(Guid.NewGuid().GetHashCode()); }
    }
}
