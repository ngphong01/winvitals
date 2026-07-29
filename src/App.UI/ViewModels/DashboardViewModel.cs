using System.Collections.ObjectModel;
using System.Windows.Input;
using App.Core;
using App.Storage.Repositories;
using AppUI.ViewModels;

namespace AppUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard page.
/// Displays system health score, drive usage, recent activity, and quick actions.
/// Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.9, 6.10
/// </summary>
public class DashboardViewModel : ViewModelBase
{
    private readonly IPerformanceAnalyzer _analyzer;
    private readonly IPerformanceRepository _perfRepo;
    private readonly ICleanRepository _cleanRepo;
    private readonly IScanRepository _scanRepo;
    private readonly IQuarantineRepository _quarantineRepo;

    public DashboardViewModel(
        IPerformanceAnalyzer analyzer,
        IPerformanceRepository perfRepo,
        ICleanRepository cleanRepo,
        IScanRepository scanRepo,
        IQuarantineRepository quarantineRepo)
    {
        _analyzer = analyzer;
        _perfRepo = perfRepo;
        _cleanRepo = cleanRepo;
        _scanRepo = scanRepo;
        _quarantineRepo = quarantineRepo;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        QuickScanCommand = new RelayCommand(() => OnQuickScanRequested?.Invoke());
    }

    // ---- Properties ----

    private double _healthScore;
    public double HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    private string _healthLabel = "Đang tải...";
    public string HealthLabel
    {
        get => _healthLabel;
        set => SetProperty(ref _healthLabel, value);
    }

    private string _totalFreed = "—";
    public string TotalFreed
    {
        get => _totalFreed;
        set => SetProperty(ref _totalFreed, value);
    }

    private int _quarantineCount;
    public int QuarantineCount
    {
        get => _quarantineCount;
        set => SetProperty(ref _quarantineCount, value);
    }

    private string _quarantineSize = "—";
    public string QuarantineSize
    {
        get => _quarantineSize;
        set => SetProperty(ref _quarantineSize, value);
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

    private double _diskPercent;
    public double DiskPercent
    {
        get => _diskPercent;
        set => SetProperty(ref _diskPercent, value);
    }

    private string _lastScan = "Chưa quét";
    public string LastScan
    {
        get => _lastScan;
        set => SetProperty(ref _lastScan, value);
    }

    private ObservableCollection<global::App.Core.DriveInfo> _drives = [];
    public ObservableCollection<global::App.Core.DriveInfo> Drives
    {
        get => _drives;
        set => SetProperty(ref _drives, value);
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    // ---- Commands ----

    public ICommand RefreshCommand { get; }
    public ICommand QuickScanCommand { get; }

    // ---- Events ----

    public event Action? OnQuickScanRequested;

    // ---- Methods ----

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            // System metrics
            var summary = await _analyzer.GetDashboardSummaryAsync();
            HealthScore = summary.HealthScore;
            HealthLabel = summary.HealthStatus;
            Drives = new ObservableCollection<global::App.Core.DriveInfo>(summary.Drives);

            // Performance snapshot
            var snap = await _analyzer.GetSnapshotAsync();
            CpuPercent = Math.Round(snap.CpuPercent, 1);
            MemoryPercent = Math.Round(snap.MemoryPercent, 1);
            DiskPercent = Math.Round(snap.DiskPercent, 1);

            // Stats from DB
            var stats = await _perfRepo.GetStatisticsAsync();
            TotalFreed = ScanItem.FormatSize(stats.TotalBytesFreed);
            QuarantineCount = stats.ActiveQuarantineItems;
            QuarantineSize = ScanItem.FormatSize(stats.QuarantinedTotalSize);

            if (stats.LastCleanDate.HasValue)
                LastScan = stats.LastCleanDate.Value.ToString("dd/MM/yyyy HH:mm");
        }
        catch (Exception ex)
        {
            App.Log.Warning(ex, "Failed to refresh dashboard");
            HealthLabel = "Lỗi tải dữ liệu";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
