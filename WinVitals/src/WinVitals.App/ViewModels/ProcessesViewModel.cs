using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.App.Services;
using WinVitals.Core.Entities;
using WinVitals.Services.Processes;

namespace WinVitals.App.ViewModels;

public sealed partial class ProcessesViewModel : ObservableObject, IDisposable
{
    private readonly IProcessService _proc;
    private readonly IAppNotifier _notify;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(2));
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<ProcessRow> Rows { get; } = new();
    public ICollectionView RowsView { get; }

    [ObservableProperty] private string filter = "";
    [ObservableProperty] private bool hideSystem = true;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private string sortBy = nameof(ProcessRow.WorkingSet);

    public ProcessesViewModel(IProcessService proc, IAppNotifier notify)
    {
        _proc = proc;
        _notify = notify;
        BindingOperations.EnableCollectionSynchronization(Rows, new object());
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = FilterRow;
        RowsView.SortDescriptions.Add(new SortDescription(nameof(ProcessRow.WorkingSet), ListSortDirection.Descending));

        _ = Task.Run(RunAsync);
    }

    partial void OnFilterChanged(string value) => RowsView.Refresh();
    partial void OnHideSystemChanged(bool value) => RowsView.Refresh();

    private bool FilterRow(object obj)
    {
        if (obj is not ProcessRow r) return false;
        if (HideSystem && r.IsSystem) return false;
        if (string.IsNullOrWhiteSpace(Filter)) return true;
        return r.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
            || (r.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private async Task RunAsync()
    {
        try
        {
            await _proc.RefreshAsync(_cts.Token);
            await Task.Delay(500, _cts.Token);

#pragma warning disable CS4014
            do
            {
                var snap = await _proc.RefreshAsync(_cts.Token);
                App.Current.Dispatcher.Invoke(() =>
                {
                    MergeInto(snap);
                    TotalCount = Rows.Count;
                });
            }
            while (await _timer.WaitForNextTickAsync(_cts.Token));
#pragma warning restore CS4014
        }
        catch (OperationCanceledException) { }
    }

    private void MergeInto(IReadOnlyList<ProcessSnapshot> snap)
    {
        var byPid = Rows.ToDictionary(r => r.Pid);
        var seen = new HashSet<int>(snap.Count);

        foreach (var s in snap)
        {
            seen.Add(s.Pid);
            if (byPid.TryGetValue(s.Pid, out var existing))
            {
                existing.Update(s);
            }
            else
            {
                Rows.Add(new ProcessRow(s));
            }
        }

        for (int i = Rows.Count - 1; i >= 0; i--)
            if (!seen.Contains(Rows[i].Pid)) Rows.RemoveAt(i);
    }

    [RelayCommand]
    private async Task EndTaskAsync(ProcessRow? row)
    {
        if (row is null) return;
        var ok = await _proc.EndProcessAsync(row.Pid);
        if (ok) _notify.Success("Process ended", $"{row.Name} (PID {row.Pid})");
        else _notify.Error("Cannot end process", $"Access denied: {row.Name}");
    }

    [RelayCommand]
    private async Task OpenLocationAsync(ProcessRow? row)
    {
        if (row is null) return;
        var ok = await _proc.OpenLocationAsync(row.Pid);
        if (!ok) _notify.Warning("Cannot open location", "File path unavailable");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
    }
}

public sealed partial class ProcessRow : ObservableObject
{
    public int Pid { get; }
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string? description;
    [ObservableProperty] private string? publisher;
    [ObservableProperty] private long workingSet;
    [ObservableProperty] private double cpuPercent;
    [ObservableProperty] private int threads;
    [ObservableProperty] private bool isSystem;
    [ObservableProperty] private string? executablePath;

    public ProcessRow(ProcessSnapshot s)
    {
        Pid = s.Pid;
        Update(s);
    }

    public void Update(ProcessSnapshot s)
    {
        Name = s.Name;
        Description = s.Description;
        Publisher = s.Publisher;
        WorkingSet = s.WorkingSetBytes;
        CpuPercent = s.CpuPercent;
        Threads = s.ThreadCount;
        IsSystem = s.IsSystem;
        ExecutablePath = s.ExecutablePath;
    }
}
