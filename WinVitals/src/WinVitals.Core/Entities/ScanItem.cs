namespace WinVitals.Core.Entities;

public sealed record ScanItem(
    string Path,
    long SizeBytes,
    DateTime LastModifiedUtc,
    ItemCategory Category,
    RiskLevel Risk,
    ItemAction RecommendedAction,
    string? MatchedRuleId = null)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public bool IsDirectory => Directory.Exists(Path);
}
