using System.Collections.ObjectModel;
using System.Windows.Input;
using App.Core;
using App.Performance;
using AppUI.ViewModels;

namespace AppUI.ViewModels;

/// <summary>
/// ViewModel for real-time performance monitoring.
/// CPU, RAM, Disk usage with historical charts.
/// Requirements: 12.1-12.10
/// </summary>
public class PerformanceViewModel : ViewModelBase
{
    private readonly IPerformanceAnalyzer _analyzer;
    private readonly IPerformanceService _perfService;

    public PerformanceViewModel(IPerformanceAnalyzer analyzer, IPerformanceService perfService)
    {
        _analyzer = analyzer;
        _perfService = perfService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        KillProcessCommand = new RelayCommand<int?>(KillProcess);
    }

    private double _cpuPercent;
    public double CpuPercent
    {
        get => _cpuPercent;
        set => SetProperty(ref _cpuPercent, value);
    }

    private double _memoryPercent;
    public double MemoryPercent
    {
        get => _memoryPercent;
        set => SetProperty(ref _memoryPercent, value);
    }

    private double _memoryUsedGB;
    public double MemoryUsedGB
    {
        get => _memoryUsedGB;
        set => SetProperty(ref _memoryUsedGB, value);
    }

    private double _memoryTotalGB;
    public double MemoryTotalGB
    {
        get => _memoryTotalGB;
        set => SetProperty(ref _memoryTotalGB, value);
    }

    private double _diskPercent;
    public double DiskPercent
    {
        get => _diskPercent;
        set => SetProperty(ref _diskPercent, value);
    }

    private string _driveInfo = "C:";
    public string DriveInfo
    {
        get => _driveInfo;
        set => SetProperty(ref _driveInfo, value);
    }

    private ObservableCollection<string> _alerts = [];
    public ObservableCollection<string> Alerts
    {
        get => _alerts;
        set => SetProperty(ref _alerts, value);
    }

    private ObservableCollection<ProcessInfo> _topProcesses = [];
    public ObservableCollection<ProcessInfo> TopProcesses
    {
        get => _topProcesses;
        set => SetProperty(ref _topProcesses, value);
    }

    private bool _isCapturing;
    public bool IsCapturing
    {
        get => _isCapturing;
        set => SetProperty(ref _isCapturing, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand KillProcessCommand { get; }

    public async Task RefreshAsync()
    {
        IsCapturing = true;
        try
        {
            var snap = await _analyzer.GetSnapshotAsync();
            CpuPercent = Math.Round(snap.CpuPercent, 1);
            MemoryPercent = Math.Round(snap.MemoryPercent, 1);
            MemoryUsedGB = Math.Round(snap.MemoryUsedGB, 1);
            MemoryTotalGB = Math.Round(snap.MemoryTotalGB, 1);
            DiskPercent = Math.Round(snap.DiskPercent, 1);
            DriveInfo = $"{snap.DriveLetter}:";

            var alerts = _perfService.CheckAlerts(snap);
            Alerts = new ObservableCollection<string>(alerts);

            var procs = await _analyzer.GetTopProcessesAsync(10);
            TopProcesses = new ObservableCollection<ProcessInfo>(procs);
        }
        catch (Exception ex)
        {
            App.Log.Warning(ex, "Failed to refresh performance");
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private async void KillProcess(int? pid)
    {
        if (pid.HasValue)
            await _analyzer.KillProcessAsync(pid.Value);
        await RefreshAsync();
    }
}
