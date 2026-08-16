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
    /// Wraps một LiteDatabaseProvider đã có (dùng chung connection với repository layer)
    /// </summary>
    public DatabaseProvider(LiteDatabaseProvider shared)
    {
        _database = shared ?? throw new ArgumentNullException(nameof(shared));
        _databasePath = shared.ConnectionString;
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
    /// Nơi duy nhất lưu file cách ly: %LocalAppData%\WindowsHealthManager\quarantine
    /// </summary>
    public static string GetQuarantineDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, AppDataFolderName, "quarantine");
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

    /// <summary>
    /// Một lần duy nhất: dời file cách ly từ [thư mục exe]\quarantine (cách cũ)
    /// sang %LocalAppData%\WindowsHealthManager\quarantine, cập nhật lại DB.
    /// </summary>
    public async Task<int> MigrateLegacyQuarantineDirectoryAsync(CancellationToken ct = default)
    {
        var oldBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "quarantine");
        if (!Directory.Exists(oldBase)) return 0;

        var newBase = GetQuarantineDirectory();
        Directory.CreateDirectory(newBase);

        var prefix = oldBase.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var items = await GetQuarantineItemsAsync();
        int moved = 0;

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            if (!item.QuarantinePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var src = item.QuarantinePath.TrimEnd('\\', '/');
            if (!File.Exists(src) && !Directory.Exists(src)) continue;

            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(newBase, fileName);
            for (int i = 1; File.Exists(dest) || Directory.Exists(dest); i++)
                dest = Path.Combine(newBase, $"{i}_{fileName}");

            try
            {
                if (File.Exists(src)) File.Move(src, dest);
                else if (Directory.Exists(src)) Directory.Move(src, dest);

                if (await Task.Run(() => _database.UpdateQuarantinePath(item.Id, dest)))
                    moved++;
            }
            catch { /* bỏ qua item không di chuyển được, không làm mất dữ liệu */ }
        }

        // File lạ không có record trong DB cũng chuyển sang — tránh bỏ sót trong folder cũ
        foreach (var entry in Directory.EnumerateFileSystemEntries(oldBase))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var fileName = Path.GetFileName(entry.TrimEnd('\\', '/'));
                var dest = Path.Combine(newBase, fileName);
                for (int i = 1; File.Exists(dest) || Directory.Exists(dest); i++)
                    dest = Path.Combine(newBase, $"{i}_{fileName}");
                if (File.Exists(entry)) File.Move(entry, dest);
                else if (Directory.Exists(entry)) Directory.Move(entry, dest);
            }
            catch { }
        }

        try { if (!Directory.EnumerateFileSystemEntries(oldBase).Any()) Directory.Delete(oldBase); } catch { }
        return moved;
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