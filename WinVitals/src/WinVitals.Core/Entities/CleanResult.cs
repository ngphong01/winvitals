namespace WinVitals.Core.Entities;

public enum CleanStatus
{
    Quarantined,
    Skipped_Blocked,
    Skipped_Missing,
    Skipped_UserChoice,
    Failed
}

public sealed record CleanOutcome(
    string Path,
    long SizeBytes,
    CleanStatus Status,
    string? ErrorMessage = null,
    Guid? QuarantineId = null);

public sealed record CleanReport(
    int TotalRequested,
    int Quarantined,
    int Skipped,
    int Failed,
    long BytesFreed,
    TimeSpan Elapsed,
    IReadOnlyList<CleanOutcome> Outcomes);
