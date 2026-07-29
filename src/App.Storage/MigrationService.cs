using LiteDB;
using App.Core;

namespace App.Storage;

/// <summary>
/// Database migration service for handling schema upgrades
/// </summary>
public class MigrationService
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<SettingDoc> _settings;

    /// <summary>
    /// Gets the current schema version
    /// </summary>
    public int CurrentVersion { get; private set; } = 1;

    /// <summary>
    /// Gets the latest schema version
    /// </summary>
    public int LatestVersion => 2;

    public MigrationService(LiteDatabase database)
    {
        _database = database;
        _settings = database.GetCollection<SettingDoc>("settings");
        
        // Get current version
        var doc = _settings.FindById("schema_version");
        CurrentVersion = doc != null && int.TryParse(doc.Value, out int version) ? version : 1;
    }

    /// <summary>
    /// Run all pending migrations
    /// </summary>
    public void Migrate()
    {
        if (CurrentVersion < 2)
        {
            MigrateToV2();
            UpdateVersion(2);
        }
    }

    /// <summary>
    /// Migrate database to version 2
    /// </summary>
    private void MigrateToV2()
    {
        // In V2, we added new indexes and improved schema
        // This migration ensures proper indexes exist
        // For now, this is a placeholder for future schema changes
        
        // Example: Add new fields if needed in future versions
        // This migration ensures backward compatibility
    }

    /// <summary>
    /// Update schema version in settings
    /// </summary>
    private void UpdateVersion(int version)
    {
        var doc = _settings.FindById("schema_version");
        
        if (doc != null)
        {
            doc.Value = version.ToString();
            _settings.Update(doc);
        }
        else
        {
            _settings.Insert(new SettingDoc { Key = "schema_version", Value = version.ToString() });
        }
        
        CurrentVersion = version;
    }

    /// <summary>
    /// Check if migration is needed
    /// </summary>
    public bool NeedsMigration() => CurrentVersion < LatestVersion;

    /// <summary>
    /// Get list of available migrations
    /// </summary>
    public List<string> GetAvailableMigrations()
    {
        var migrations = new List<string>();
        
        if (CurrentVersion < 2)
        {
            migrations.Add("V2: Add improved index structure and schema");
        }
        
        return migrations;
    }

    #region Document Classes

    public class SettingDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ScanItemDoc
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

    #endregion
}
