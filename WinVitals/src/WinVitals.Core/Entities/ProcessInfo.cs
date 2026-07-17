namespace WinVitals.Core.Entities;

public sealed record ProcessSnapshot(
    int Pid,
    string Name,
    string? Description,
    string? Publisher,
    long WorkingSetBytes,
    double CpuPercent,
    int ThreadCount,
    DateTime StartTimeUtc,
    string? ExecutablePath,
    bool IsSystem,
    bool IsElevated);
