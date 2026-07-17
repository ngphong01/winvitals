namespace WinVitals.Core.Entities;

public readonly record struct PerfSample(
    DateTime TimestampUtc,
    double CpuPercent,
    double RamUsedGb,
    double RamTotalGb,
    double DiskReadMBps,
    double DiskWriteMBps,
    int HealthScore)
{
    public double RamPercent => RamTotalGb <= 0 ? 0 : RamUsedGb / RamTotalGb * 100.0;
}
