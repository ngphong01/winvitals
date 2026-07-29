using System.Collections.ObjectModel;
using System.Windows.Input;
using App.Core;
using AppUI.ViewModels;

namespace AppUI.ViewModels;

/// <summary>
/// ViewModel for Quarantine management — list, filter, restore, delete.
/// Requirements: 13.3-13.10
/// </summary>
public class QuarantineViewModel : ViewModelBase
{
    private readonly IQuarantineService _quarantineService;

    public QuarantineViewModel(IQuarantineService quarantineService)
    {
        _quarantineService = quarantineService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        RestoreCommand = new RelayCommand<int?>(RestoreItem);
        DeleteCommand = new RelayCommand<int?>(DeleteItem);
        RestoreAllCommand = new AsyncRelayCommand(RestoreAllAsync);
        CleanupExpiredCommand = new AsyncRelayCommand(CleanupExpiredAsync);
    }

    private ObservableCollection<QuarantineItem> _items = [];
    public ObservableCollection<QuarantineItem> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    private int _totalItems;
    public int TotalItems
    {
        get => _totalItems;
        set => SetProperty(ref _totalItems, value);
    }

    private string _totalSize = "—";
    public string TotalSize
    {
        get => _totalSize;
        set => SetProperty(ref _totalSize, value);
    }

    private QuarantineStatus? _filterStatus = QuarantineStatus.Active;
    public QuarantineStatus? FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
                _ = RefreshAsync();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand CleanupExpiredCommand { get; }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _quarantineService.ListAsync(FilterStatus);
            Items = new ObservableCollection<QuarantineItem>(items);
            TotalItems = items.Count;
            TotalSize = ScanItem.FormatSize(items.Sum(i => i.SizeBytes));
        }
        catch (Exception ex) { App.Log.Warning(ex, "Failed to refresh quarantine"); }
        finally { IsLoading = false; }
    }

    private async void RestoreItem(int? id)
    {
        if (id.HasValue)
        {
            await _quarantineService.RestoreAsync([id.Value]);
            await RefreshAsync();
        }
    }

    private async void DeleteItem(int? id)
    {
        if (id.HasValue)
        {
            await _quarantineService.PermanentDeleteAsync([id.Value]);
            await RefreshAsync();
        }
    }

    private async Task RestoreAllAsync()
    {
        var ids = Items.Where(i => i.Status == QuarantineStatus.Active).Select(i => i.Id);
        await _quarantineService.RestoreAsync(ids);
        await RefreshAsync();
    }

    private async Task CleanupExpiredAsync()
    {
        await _quarantineService.CleanupExpiredAsync();
        await RefreshAsync();
    }
}
