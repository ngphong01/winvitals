using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.Core.Entities;
using WinVitals.Core;
using WinVitals.Services.Cleaning;
using WinVitals.Services.Scanning;

namespace WinVitals.App.ViewModels;

public sealed partial class CleanViewModel : ObservableObject
{
    private readonly IScanService _scan;
    private readonly ICleanService _clean;
    private CancellationTokenSource? _cts;

    public ObservableCollection<CleanItemVm> Items { get; } = new();

    [ObservableProperty] private int currentStep = 1;
    [ObservableProperty] private ScanPreset selectedPreset = ScanPreset.Quick;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private int scannedCount;
    [ObservableProperty] private long selectedBytes;
    [ObservableProperty] private int selectedCount;
    [ObservableProperty] private int confirmCountdown;
    [ObservableProperty] private bool canConfirm;
    [ObservableProperty] private CleanReport? lastReport;

    public CleanViewModel(IScanService scan, ICleanService clean)
    {
        _scan = scan;
        _clean = clean;
        BindingOperations.EnableCollectionSynchronization(Items, new object());
    }

    partial void OnSelectedPresetChanged(ScanPreset value)
    {
        Items.Clear();
        RecomputeSelection();
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task NextAsync()
    {
        switch (CurrentStep)
        {
            case 1:
                await ScanForPreviewAsync();
                CurrentStep = 2;
                break;
            case 2:
                CurrentStep = 3;
                await StartConfirmCountdownAsync();
                break;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 1) CurrentStep--;
        CanConfirm = false;
        _cts?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteClean))]
    private async Task ConfirmAsync()
    {
        IsBusy = true; Status = "Cleaning...";
        _cts = new CancellationTokenSource();
        try
        {
            var selected = Items.Where(x => x.IsSelected).Select(x => x.Item).ToList();
            var progress = new Progress<CleanProgress>(p =>
                Status = $"Cleaning [{p.Processed}/{p.Total}] {Path.GetFileName(p.CurrentPath)}");

            LastReport = await Task.Run(() => _clean.ExecuteAsync(selected, progress, _cts.Token));
            Status = $"Done. Quarantined {LastReport.Quarantined}, " +
                     $"skipped {LastReport.Skipped}, freed {FormatBytes(LastReport.BytesFreed)}";
            CurrentStep = 4;
        }
        catch (OperationCanceledException) { Status = "Cancelled"; }
        catch (Exception ex) { Status = "Error: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Restart()
    {
        Items.Clear();
        LastReport = null;
        CurrentStep = 1;
        Status = "";
        CanConfirm = false;
    }

    public void ToggleAll(bool select)
    {
        foreach (var i in Items)
        {
            if (i.IsBlocked) continue;
            i.IsSelected = select;
        }
        RecomputeSelection();
    }

    private async Task ScanForPreviewAsync()
    {
        Items.Clear();
        IsBusy = true; Status = "Scanning..."; ScannedCount = 0;
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ScannedCount = p.ItemsFound;
                Status = $"Scanning... {p.ItemsFound} items";
            });

            await Task.Run(async () =>
            {
                await foreach (var item in _scan.ScanAsync(SelectedPreset, progress, _cts.Token))
                {
                    Items.Add(new CleanItemVm(item, RecomputeSelection));
                }
            });

            foreach (var vm in Items)
            {
                if (vm.IsBlocked) continue;
                vm.IsSelected = vm.Item.Risk is RiskLevel.Safe or RiskLevel.Low;
            }
            RecomputeSelection();
            Status = $"Preview ready. {Items.Count} items found.";
        }
        catch (OperationCanceledException) { Status = "Cancelled"; }
        finally { IsBusy = false; }
    }

    private async Task StartConfirmCountdownAsync()
    {
        CanConfirm = false;
        for (int i = 5; i > 0; i--)
        {
            ConfirmCountdown = i;
            await Task.Delay(1000);
        }
        ConfirmCountdown = 0;
        CanConfirm = true;
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private void RecomputeSelection()
    {
        SelectedCount = Items.Count(x => x.IsSelected);
        SelectedBytes = Items.Where(x => x.IsSelected).Sum(x => x.Item.SizeBytes);
    }

    private bool NotBusy() => !IsBusy;
    private bool CanExecuteClean() => CanConfirm && !IsBusy && SelectedCount > 0;

    partial void OnIsBusyChanged(bool value)
    {
        NextCommand.NotifyCanExecuteChanged();
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    private static string FormatBytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB" }; double s = b; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.##} {u[i]}";
    }
}

public sealed partial class CleanItemVm : ObservableObject
{
    private readonly Action _onChanged;
    public ScanItem Item { get; }
    public string Name => Item.Name;
    public long SizeBytes => Item.SizeBytes;
    public ItemCategory Category => Item.Category;
    public RiskLevel Risk => Item.Risk;
    public bool IsBlocked => Item.RecommendedAction == ItemAction.Block;

    [ObservableProperty] private bool isSelected;

    partial void OnIsSelectedChanged(bool value) => _onChanged();

    public CleanItemVm(ScanItem item, Action onChanged)
    {
        Item = item;
        _onChanged = onChanged;
        IsSelected = false;
    }
}
