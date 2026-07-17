namespace WinVitals.Core.Entities;

public sealed record SystemInfo(
    string MachineName,
    string UserName,
    string OsVersion,
    string OsArchitecture,
    string CpuName,
    int CpuCores,
    int CpuLogicalProcessors,
    double RamTotalGb,
    string DotNetVersion,
    string GpuName);
