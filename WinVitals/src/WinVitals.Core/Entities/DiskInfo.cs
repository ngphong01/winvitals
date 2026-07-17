namespace WinVitals.Core.Entities;

public sealed record DriveUsage(
    string Name,
    string Label,
    string DriveFormat,
    long TotalBytes,
    long FreeBytes,
    double UsedPercent);

public sealed record SmartInfo(
    string Model,
    string SerialNumber,
    string InterfaceType,
    string Status,
    bool PredictFailure,
    uint? TemperatureCelsius,
    ulong? PowerOnHours);
