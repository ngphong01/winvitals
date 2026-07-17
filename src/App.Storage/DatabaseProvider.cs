using Microsoft.Data.Sqlite;
using App.Core;

namespace App.Storage;

/// <summary>
/// SQLite database provider cho Windows Health Manager
/// </summary>
public class DatabaseProvider : IStorageProvider, IDisposable
{
    private readonly string _connectionString;

    public DatabaseProvider(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS scan_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scan_type TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT,
                total_items INTEGER DEFAULT 0,
                total_size INTEGER DEFAULT 0,
                drives_scanned TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS scan_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER,
                path TEXT NOT NULL,
                name TEXT NOT NULL,
                size_bytes INTEGER DEFAULT 0,
                is_directory INTEGER DEFAULT 0,
                category TEXT DEFAULT 'Unknown',
                risk_level TEXT DEFAULT 'Unknown',
                action TEXT DEFAULT 'WarnDelete',
                extension TEXT DEFAULT '',
                app_origin TEXT,
                matched_rule TEXT DEFAULT '',
                last_modified TEXT,
                FOREIGN KEY(session_id) REFERENCES scan_sessions(id)
            );

            CREATE TABLE IF NOT EXISTS clean_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                clean_date TEXT NOT NULL,
                clean_level TEXT NOT NULL,
                items_cleaned INTEGER DEFAULT 0,
                space_freed INTEGER DEFAULT 0,
                items_quarantined INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS quarantine_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                original_path TEXT NOT NULL,
                quarantine_path TEXT NOT NULL,
                file_name TEXT NOT NULL,
                size_bytes INTEGER DEFAULT 0,
                quarantine_date TEXT NOT NULL,
                restore_date TEXT,
                expiry_date TEXT NOT NULL,
                status TEXT DEFAULT 'Active',
                reason TEXT DEFAULT '',
                source_module TEXT DEFAULT '',
                risk_level TEXT DEFAULT 'Medium'
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS performance_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                cpu_percent REAL DEFAULT 0,
                memory_percent REAL DEFAULT 0,
                disk_percent REAL DEFAULT 0,
                health_score REAL DEFAULT 0,
                top_cpu_process TEXT DEFAULT '',
                top_mem_process TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS ignored_paths (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                reason TEXT DEFAULT '',
                added_date TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
    }

    // ========== Scan Sessions ==========
    public async Task SaveScanSessionAsync(ScanSession session)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO scan_sessions
            (scan_type, start_time, end_time, total_items, total_size, drives_scanned)
            VALUES (@t, @s, @e, @i, @sz, @d)";
        cmd.Parameters.AddWithValue("@t", session.ScanType.ToString());
        cmd.Parameters.AddWithValue("@s", session.StartTime.ToString("o"));
        cmd.Parameters.AddWithValue("@e", (session.EndTime?.ToString("o")) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@i", session.TotalItemsFound);
        cmd.Parameters.AddWithValue("@sz", session.TotalSizeBytes);
        cmd.Parameters.AddWithValue("@d", string.Join(",", session.DrivesScanned));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ScanSession>> GetScanHistoryAsync(int days = 30)
    {
        var sessions = new List<ScanSession>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT id, scan_type, start_time, end_time, total_items, total_size, drives_scanned
            FROM scan_sessions WHERE start_time >= @cutoff ORDER BY start_time DESC";
        cmd.Parameters.AddWithValue("@cutoff", DateTime.Now.AddDays(-days).ToString("o"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new ScanSession
            {
                Id = reader.GetInt32(0),
                ScanType = Enum.Parse<ScanType>(reader.GetString(1)),
                StartTime = DateTime.Parse(reader.GetString(2)),
                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                TotalItemsFound = reader.GetInt64(4),
                TotalSizeBytes = reader.GetInt64(5),
                DrivesScanned = reader.GetString(6).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            });
        }
        return sessions;
    }

    public async Task<AppStatistics> GetStatisticsAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                (SELECT COUNT(*) FROM scan_sessions),
                (SELECT COUNT(*) FROM clean_history),
                (SELECT COALESCE(SUM(space_freed), 0) FROM clean_history),
                (SELECT COUNT(*) FROM quarantine_items WHERE status='Active'),
                (SELECT COALESCE(SUM(size_bytes), 0) FROM quarantine_items WHERE status='Active'),
                (SELECT COALESCE(COUNT(*), 0) FROM scan_items WHERE action='Block'),
                (SELECT MAX(start_time) FROM scan_sessions),
                (SELECT MAX(clean_date) FROM clean_history)
        ";
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AppStatistics
            {
                TotalScans = reader.GetInt32(0),
                TotalCleans = reader.GetInt32(1),
                TotalSpaceFreed = reader.GetInt64(2),
                QuarantineItemCount = reader.GetInt32(3),
                QuarantineTotalSize = reader.GetInt64(4),
                BlockedItemsCount = reader.GetInt32(5),
                LastScanDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                LastCleanDate = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7))
            };
        }
        return new AppStatistics();
    }

    // ========== Clean History ==========
    public async Task SaveCleanHistoryAsync(CleanHistory history)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO clean_history (clean_date, clean_level, items_cleaned, space_freed, items_quarantined)
            VALUES (@d, @l, @i, @s, @q)";
        cmd.Parameters.AddWithValue("@d", history.CleanDate.ToString("o"));
        cmd.Parameters.AddWithValue("@l", history.CleanLevel.ToString());
        cmd.Parameters.AddWithValue("@i", history.ItemsCleaned);
        cmd.Parameters.AddWithValue("@s", history.SpaceFreedBytes);
        cmd.Parameters.AddWithValue("@q", history.ItemsInQuarantine);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CleanHistory>> GetCleanHistoryAsync(int days = 30)
    {
        var history = new List<CleanHistory>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT id, clean_date, clean_level, items_cleaned, space_freed, items_quarantined
            FROM clean_history WHERE clean_date >= @cutoff ORDER BY clean_date DESC";
        cmd.Parameters.AddWithValue("@cutoff", DateTime.Now.AddDays(-days).ToString("o"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(new CleanHistory
            {
                Id = reader.GetInt32(0),
                CleanDate = DateTime.Parse(reader.GetString(1)),
                CleanLevel = Enum.Parse<CleanLevel>(reader.GetString(2)),
                ItemsCleaned = reader.GetInt32(3),
                SpaceFreedBytes = reader.GetInt64(4),
                ItemsInQuarantine = reader.GetInt32(5)
            });
        }
        return history;
    }

    // ========== Quarantine ==========
    public async Task SaveQuarantineItemAsync(QuarantineItem item)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO quarantine_items
            (original_path, quarantine_path, file_name, size_bytes, quarantine_date,
             restore_date, expiry_date, status, reason, source_module, risk_level)
            VALUES (@op, @qp, @fn, @sz, @qd, @rd, @ed, @st, @rs, @sm, @rl)";
        cmd.Parameters.AddWithValue("@op", item.OriginalPath);
        cmd.Parameters.AddWithValue("@qp", item.QuarantinePath);
        cmd.Parameters.AddWithValue("@fn", item.FileName);
        cmd.Parameters.AddWithValue("@sz", item.SizeBytes);
        cmd.Parameters.AddWithValue("@qd", item.QuarantineDate.ToString("o"));
        cmd.Parameters.AddWithValue("@rd", (item.RestoreDate?.ToString("o")) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ed", item.ExpiryDate.ToString("o"));
        cmd.Parameters.AddWithValue("@st", item.Status.ToString());
        cmd.Parameters.AddWithValue("@rs", item.Reason);
        cmd.Parameters.AddWithValue("@sm", item.SourceModule);
        cmd.Parameters.AddWithValue("@rl", item.Risk.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<QuarantineItem>> GetQuarantineItemsAsync()
    {
        var items = new List<QuarantineItem>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT id, original_path, quarantine_path, file_name, size_bytes,
            quarantine_date, restore_date, expiry_date, status, reason, source_module, risk_level
            FROM quarantine_items ORDER BY quarantine_date DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new QuarantineItem
            {
                Id = reader.GetInt32(0),
                OriginalPath = reader.GetString(1),
                QuarantinePath = reader.GetString(2),
                FileName = reader.GetString(3),
                SizeBytes = reader.GetInt64(4),
                QuarantineDate = DateTime.Parse(reader.GetString(5)),
                RestoreDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                ExpiryDate = DateTime.Parse(reader.GetString(7)),
                Status = Enum.Parse<QuarantineStatus>(reader.GetString(8)),
                Reason = reader.GetString(9),
                SourceModule = reader.GetString(10),
                Risk = Enum.Parse<RiskLevel>(reader.GetString(11))
            });
        }
        return items;
    }

    public async Task<bool> RemoveQuarantineItemAsync(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM quarantine_items WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateQuarantineStatusAsync(int id, QuarantineStatus status)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE quarantine_items SET status=@s WHERE id=@id";
        cmd.Parameters.AddWithValue("@s", status.ToString());
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ========== Settings ==========
    public async Task<string?> GetSettingAsync(string key)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key=@k";
        cmd.Parameters.AddWithValue("@k", key);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@k, @v)";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose() { }
}
