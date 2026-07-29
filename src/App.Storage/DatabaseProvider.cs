using App.Core;

namespace App.Storage;

/// <summary>
/// LiteDB database provider for Windows Health Manager
/// Handles database initialization and configuration with proper location in user's app data folder
/// </summary>
public class DatabaseProvider : IStorageProvider, IDisposable
{
    private readonly LiteDatabaseProvider _database;
    private readonly string _databasePath;

    /// <summary>
    /// Default database filename
    /// </summary>
    public const string DefaultDatabaseName = "whm.db";

    /// <summary>
    /// Application folder name in AppData
    /// </summary>
    public const string AppDataFolderName = "WindowsHealthManager";

    /// <summary>
    /// Gets the database file path
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Initializes a new instance of DatabaseProvider with custom path
    /// </summary>
    /// <param name="dbPath">Custom database path. If null or empty, uses default app data location.</param>
    public DatabaseProvider(string? dbPath = null)
    {
        _databasePath = GetDatabasePath(dbPath);
        _database = new LiteDatabaseProvider(_databasePath);
    }

    /// <summary>
    /// Gets the database path, defaulting to user's app data folder if not specified
    /// </summary>
    /// <param name="customPath">Custom path if provided</param>
    /// <returns>Resolved database path</returns>
    private static string GetDatabasePath(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return customPath;
        }

        // Use %LOCALAPPDATA%\WindowsHealthManager\whm.db as default location
        // Requirements 2.1, 2.4, 2.8: Database file location in user's app data folder
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, AppDataFolderName);
        
        // Ensure directory exists
        Directory.CreateDirectory(appFolder);
        
        return Path.Combine(appFolder, DefaultDatabaseName);
    }

    /// <summary>
    /// Gets the default database path in user's app data folder
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        return GetDatabasePath(null);
    }

    /// <summary>
    /// Disposes the database provider and closes database connection
    /// Requirement 2.8: Database connection properly closed when application shuts down
    /// </summary>
    public void Dispose()
    {
        _database?.Dispose();
    }

    public async Task SaveScanSessionAsync(ScanSession session)
    {
        await Task.Run(() => _database.SaveScanSession(session));
    }

    public async Task<List<ScanSession>> GetScanHistoryAsync(int days = 30)
    {
        return await Task.Run(() => _database.GetScanHistory(days));
    }

    public async Task<AppStatistics> GetStatisticsAsync()
    {
        return await Task.Run(() => _database.GetStatistics());
    }

    public async Task SaveCleanHistoryAsync(CleanHistory history)
    {
        await Task.Run(() => _database.SaveCleanHistory(history));
    }

    public async Task<List<CleanHistory>> GetCleanHistoryAsync(int days = 30)
    {
        return await Task.Run(() => _database.GetCleanHistory(days));
    }

    public async Task SaveQuarantineItemAsync(QuarantineItem item)
    {
        await Task.Run(() => _database.SaveQuarantineItem(item));
    }

    public async Task<List<QuarantineItem>> GetQuarantineItemsAsync()
    {
        return await Task.Run(() => _database.GetQuarantineItems());
    }

    public async Task<bool> RemoveQuarantineItemAsync(int id)
    {
        return await Task.Run(() => _database.RemoveQuarantineItem(id));
    }

    public async Task<bool> UpdateQuarantineStatusAsync(int id, QuarantineStatus status)
    {
        return await Task.Run(() => _database.UpdateQuarantineStatus(id, status));
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        return await Task.Run(() => _database.GetSetting(key));
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await Task.Run(() => _database.SetSetting(key, value));
    }
}