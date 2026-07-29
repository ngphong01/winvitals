using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using App.Core;
using AppUI.ViewModels;

namespace AppUI.ViewModels;

/// <summary>
/// ViewModel for scanning operations — configure, run, view results.
/// Requirements: 6.9, 10.1, 10.5, 11.1-11.10
/// </summary>
public class ScannerViewModel : ViewModelBase
{
    private readonly IScannerService _scannerService;

    public ScannerViewModel(IScannerService scannerService)
    {
        _scannerService = scannerService;

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(Cancel);
        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
    }

    private ObservableCollection<ScanItem> _results = [];
    public ObservableCollection<ScanItem> Results
    {
        get => _results;
        set => SetProperty(ref _results, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    private string _status = "Sẵn sàng";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private string _selectedDrive = "C:";
    public string SelectedDrive
    {
        get => _selectedDrive;
        set => SetProperty(ref _selectedDrive, value);
    }

    private ScanType _selectedScanType = ScanType.Quick;
    public ScanType SelectedScanType
    {
        get => _selectedScanType;
        set => SetProperty(ref _selectedScanType, value);
    }

    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                ApplyFilter();
        }
    }

    private ObservableCollection<ScanItem> _filteredResults = [];
    public ObservableCollection<ScanItem> FilteredResults
    {
        get => _filteredResults;
        set => SetProperty(ref _filteredResults, value);
    }

    private CancellationTokenSource? _cts;
    private List<ScanItem> _allResults = [];

    public ICommand ScanCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }

    public ObservableCollection<string> AvailableDrives { get; } =
        new(Environment.GetLogicalDrives().Where(d => Directory.Exists(d)));

    public static List<ScanType> AvailableScanTypes { get; } =
        [ScanType.Quick, ScanType.Deep, ScanType.Developer];

    private async Task ScanAsync()
    {
        IsScanning = true;
        _allResults.Clear();
        Results.Clear();
        FilteredResults.Clear();
        ProgressPercent = 0;

        _cts = new CancellationTokenSource();

        try
        {
            var options = new ScanOptions
            {
                Drives = [SelectedDrive],
                Type = SelectedScanType,
                IncludeCloud = true,
                IncludeDeveloper = SelectedScanType == ScanType.Developer
            };

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Status = p.Status;
                ProgressPercent = p.Progress;
            });

            Status = "Đang quét...";
            _allResults = await _scannerService.ScanAsync(options, progress, _cts.Token);
            Results = new ObservableCollection<ScanItem>(_allResults.OrderByDescending(i => i.SizeBytes));
            ApplyFilter();

            Status = $"Tìm thấy {_allResults.Count} items ({ScanItem.FormatSize(_allResults.Sum(i => i.SizeBytes))})";
        }
        catch (OperationCanceledException)
        {
            Status = "Đã hủy";
        }
        catch (Exception ex)
        {
            Status = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel() => _cts?.Cancel();

    private void SelectAll()
    {
        foreach (var item in FilteredResults)
            item.RecommendedAction = ItemAction.WarnDelete;
    }

    private void DeselectAll()
    {
        foreach (var item in FilteredResults)
            item.RecommendedAction = ItemAction.Skip;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            FilteredResults = new ObservableCollection<ScanItem>(_allResults.OrderByDescending(i => i.SizeBytes));
        else
            FilteredResults = new ObservableCollection<ScanItem>(
                _allResults.Where(i =>
                    i.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    i.Path.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    i.Category.ToString().Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.SizeBytes));
    }
}
