using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.Core.Entities;
using WinVitals.Services.Quarantine;

namespace WinVitals.App.ViewModels;

public sealed partial class QuarantineViewModel : ObservableObject
{
    private readonly IQuarantineService _q;

    public ObservableCollection<QuarantineEntry> Entries { get; } = new();

    [ObservableProperty] private long totalBytes;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private QuarantineEntry? selectedEntry;

    public QuarantineViewModel(IQuarantineService q)
    {
        _q = q;
        BindingOperations.EnableCollectionSynchronization(Entries, new object());
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Entries.Clear();
        foreach (var e in _q.GetActive()) Entries.Add(e);
        TotalBytes = _q.TotalQuarantinedBytes;
        Status = $"{Entries.Count} active items";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedEntry is null) return;
        var ok = await _q.RestoreAsync(SelectedEntry.Id);
        Status = ok ? $"Restored {Path.GetFileName(SelectedEntry.OriginalPath)}" : "Restore failed";
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PurgeSelectedAsync()
    {
        if (SelectedEntry is null) return;
        var ok = await _q.PurgeAsync(SelectedEntry.Id);
        Status = ok ? "Purged" : "Purge failed";
        Refresh();
    }

    [RelayCommand]
    private async Task PurgeExpiredAsync()
    {
        var n = await _q.PurgeExpiredAsync();
        Status = $"Purged {n} expired";
        Refresh();
    }

    private bool HasSelection() => SelectedEntry is not null;

    partial void OnSelectedEntryChanged(QuarantineEntry? value)
    {
        RestoreSelectedCommand.NotifyCanExecuteChanged();
        PurgeSelectedCommand.NotifyCanExecuteChanged();
    }
}
