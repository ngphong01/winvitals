using App.Core;

namespace App.Core;

/// <summary>
/// Interface cho các scanner
/// </summary>
public interface IScanner
{
    string Name { get; }
    ScanType ScanType { get; }
    Task<List<ScanItem>> ScanAsync(
        IEnumerable<string> drives,
        IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Interface cho các cleaner
/// </summary>
public interface ICleaner
{
    string Name { get; }
    CleanLevel CleanLevel { get; }
    Task<(long FreedBytes, int ItemsProcessed, List<string> Errors)> CleanAsync(
        IEnumerable<ScanItem> items,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Interface cho storage (database + files)
/// </summary>
public interface IStorageProvider
{
    // Scan sessions
    Task SaveScanSessionAsync(ScanSession session);
    Task<List<ScanSession>> GetScanHistoryAsync(int days = 30);
    Task<AppStatistics> GetStatisticsAsync();

    // Clean history
    Task SaveCleanHistoryAsync(CleanHistory history);
    Task<List<CleanHistory>> GetCleanHistoryAsync(int days = 30);

    // Quarantine
    Task SaveQuarantineItemAsync(QuarantineItem item);
    Task<List<QuarantineItem>> GetQuarantineItemsAsync();
    Task<bool> RemoveQuarantineItemAsync(int id);
    Task<bool> UpdateQuarantineStatusAsync(int id, QuarantineStatus status);

    // Settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
}

/// <summary>
/// Interface cho Rule Engine
/// </summary>
public interface IRuleEngine
{
    Task LoadRulesAsync();
    List<Rule> GetRules(CleanLevel? level = null);
    (ItemAction Action, RiskLevel Risk, string MatchedRule) Evaluate(
        string path, long sizeBytes = 0, DateTime? lastModified = null);
    void AddRule(Rule rule);
    bool RemoveRule(string ruleId);
    Task<bool> ToggleRule(string ruleId);
    bool UpdateRule(string ruleId, Rule updated);
    Task SaveRulesAsync();
}

/// <summary>
/// Interface cho Risk Engine
/// </summary>
public interface IRiskEngine
{
    RiskLevel AssessRisk(string path, ItemCategory category,
        long sizeBytes = 0, DateTime? lastModified = null);
    bool IsProtected(string path);
    List<string> GetProtectedPaths();
}

/// <summary>
/// Interface cho Performance Analyzer
/// </summary>
public interface IPerformanceAnalyzer
{
    Task<PerformanceSnapshot> GetSnapshotAsync();
    Task<List<ProcessInfo>> GetTopProcessesAsync(int topN = 20);
    Task<List<StartupEntry>> GetStartupEntriesAsync();
    Task<bool> DisableStartupEntryAsync(StartupEntry entry);
    Task<bool> EnableStartupEntryAsync(StartupEntry entry);
    Task<bool> KillProcessAsync(int pid);
    double CalculateHealthScore(PerformanceSnapshot snapshot);
    Task<DashboardSummary> GetDashboardSummaryAsync();
}
