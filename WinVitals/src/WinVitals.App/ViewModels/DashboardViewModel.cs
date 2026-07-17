using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.Core.Entities;
using WinVitals.Services.Disks;
using WinVitals.Services.Metrics;

namespace WinVitals.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IDiskService _disks;

    public ObservableCollection<DriveUsage> Drives { get; } = new();
    public ObservableCollection<SmartInfo> SmartDrives { get; } = new();

    [ObservableProperty] private int healthScore = 100;
    [ObservableProperty] private double cpuPercent;
    [ObservableProperty] private double ramPercent;
    [ObservableProperty] private bool isLoadingSmart;
    [ObservableProperty] private SystemInfo? sysInfo;

    public DashboardViewModel(IMetricsService metrics, IDiskService disks)
    {
        _disks = disks;

        LoadSystemInfo();

        _ = Task.Run(async () =>
        {
            await foreach (var s in metrics.Stream.ReadAllAsync())
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    HealthScore = s.HealthScore;
                    CpuPercent = s.CpuPercent;
                    RamPercent = s.RamPercent;
                });
            }
        });

        RefreshDrives();
        _ = LoadSmartAsync();
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var d in _disks.GetDrives()) Drives.Add(d);
    }

    [RelayCommand]
    private async Task LoadSmartAsync()
    {
        IsLoadingSmart = true;
        try
        {
            var list = await _disks.GetSmartAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                SmartDrives.Clear();
                foreach (var s in list) SmartDrives.Add(s);
            });
        }
        finally { IsLoadingSmart = false; }
    }

    private void LoadSystemInfo()
    {
        try
        {
            var cpu = "Unknown";
            int cores = Environment.ProcessorCount;
            int logical = Environment.ProcessorCount;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    cpu = obj["Name"]?.ToString()?.Trim() ?? cpu;
                    if (obj["NumberOfCores"] is uint c) cores = (int)c;
                    if (obj["NumberOfLogicalProcessors"] is uint l) logical = (int)l;
                    break;
                }
            }
            catch { }

            var gpu = "Unknown";
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    gpu = obj["Name"]?.ToString()?.Trim() ?? gpu;
                    break;
                }
            }
            catch { }

            // Get total RAM from system (avoid depending on MetricsService stream)
            double ramTotal = 0;
            try
            {
                var mem = GC.GetGCMemoryInfo();
                // Use WMI for accurate total RAM
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    if (obj["TotalVisibleMemorySize"] is ulong kb)
                        ramTotal = kb / 1024.0 / 1024.0; // KB to GB
                    break;
                }
            }
            catch { }

            SysInfo = new SystemInfo(
                MachineName: Environment.MachineName,
                UserName: Environment.UserName,
                OsVersion: RuntimeInformation.OSDescription,
                OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
                CpuName: cpu,
                CpuCores: cores,
                CpuLogicalProcessors: logical,
                RamTotalGb: Math.Round(ramTotal, 1),
                DotNetVersion: Environment.Version.ToString(),
                GpuName: gpu
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Dashboard] LoadSystemInfo error: {ex.Message}");
        }
    }
}
