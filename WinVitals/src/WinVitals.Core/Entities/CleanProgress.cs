namespace WinVitals.Core.Entities;

public sealed record CleanProgress(
    string CurrentPath,
    int Processed,
    int Total,
    long BytesFreed);
