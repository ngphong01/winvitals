namespace App.Core;

/// <summary>
/// Value object representing a risk assessment result.
/// Immutable record type.
/// Requirements: 1.2, 1.7
/// </summary>
public sealed record RiskAssessment
{
    public RiskLevel Level { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string MatchedRule { get; init; } = string.Empty;
    public bool IsProtected { get; init; }
    public DateTime AssessedAt { get; init; } = DateTime.Now;

    public static RiskAssessment Safe(string? rule = null) => new()
    {
        Level = RiskLevel.Safe, Reason = "Safe to delete", MatchedRule = rule ?? "built-in"
    };

    public static RiskAssessment Blocked(string reason, string? rule = null) => new()
    {
        Level = RiskLevel.Critical, Reason = reason, IsProtected = true, MatchedRule = rule ?? "built-in-protected"
    };

    public static RiskAssessment Warn(RiskLevel level, string reason, string? rule = null) => new()
    {
        Level = level, Reason = reason, MatchedRule = rule ?? "built-in"
    };
}

/// <summary>
/// Value object representing a recommended action for a scan item.
/// Requirements: 1.2, 1.7
/// </summary>
public sealed record ActionRecommendation
{
    public ItemAction Action { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Suggestion { get; init; } = string.Empty;
    public bool RequiresConfirmation { get; init; }

    public static ActionRecommendation SafeDelete(string suggestion = "") => new()
    {
        Action = ItemAction.SafeDelete, Description = "Có thể xóa an toàn", Suggestion = suggestion
    };

    public static ActionRecommendation WarnDelete(string suggestion = "") => new()
    {
        Action = ItemAction.WarnDelete, Description = "Nên kiểm tra trước khi xóa",
        Suggestion = suggestion, RequiresConfirmation = true
    };

    public static ActionRecommendation QuarantineItem(string suggestion = "") => new()
    {
        Action = ItemAction.Quarantine, Description = "Chuyển vào quarantine",
        Suggestion = suggestion, RequiresConfirmation = true
    };

    public static ActionRecommendation Block(string reason) => new()
    {
        Action = ItemAction.Block, Description = "Không xóa", Suggestion = reason
    };

    public static ActionRecommendation Skip => new()
    {
        Action = ItemAction.Skip, Description = "Bỏ qua"
    };
}

/// <summary>
/// Value object for scan configuration options.
/// Requirements: 1.2, 1.7
/// </summary>
public sealed record ScanOptions
{
    public List<string> Drives { get; init; } = [];
    public ScanType Type { get; init; } = ScanType.Quick;
    public bool IncludeCloud { get; init; }
    public bool IncludeBrowser { get; init; }
    public bool IncludeDeveloper { get; set; }
    public bool IncludeWindowsStore { get; init; }
    public long MinFileSizeBytes { get; init; } = 1_048_576; // 1MB
    public List<string> CustomPaths { get; init; } = [];
    public int MaxConcurrency { get; init; } = 4;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Value object for clean operation configuration.
/// Requirements: 1.2, 1.7
/// </summary>
public sealed record CleanOptions
{
    public CleanLevel Level { get; init; } = CleanLevel.Quick;
    public bool PreviewOnly { get; init; } = true;
    public bool UseQuarantine { get; init; } = true;
    public TimeSpan QuarantineExpiry { get; init; } = TimeSpan.FromDays(14);
    public bool RemoveEmptyDirectories { get; init; } = true;
    public List<string> ExcludedPaths { get; init; } = [];
}
