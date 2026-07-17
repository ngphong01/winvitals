using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Services.Scanning;

namespace WinVitals.App.ViewModels;

public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IScanService _scan;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ScanItem> Items { get; } = new();

    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private long totalBytes;
    [ObservableProperty] private int totalItems;
    [ObservableProperty] private ScanPreset selectedPreset = ScanPreset.Quick;
    [ObservableProperty] private string currentPath = "";
    [ObservableProperty] private string foundSizeText = "";
    [ObservableProperty] private string elapsedText = "";

    public ScanViewModel(IScanService scan)
    {
        _scan = scan;
        BindingOperations.EnableCollectionSynchronization(Items, new object());
        Debug.WriteLine($"[ScanVM] Created, ScanService={scan.GetType().Name}");
    }

    /// <summary>Public method for code-behind direct call.</summary>
    public async Task StartScan()
    {
        Debug.WriteLine($"[ScanVM] StartScan() called, preset={SelectedPreset}, IsScanning={IsScanning}");
        if (IsScanning) return;

        Items.Clear();
        TotalBytes = 0; TotalItems = 0;
        CurrentPath = "Initializing...";
        FoundSizeText = "0 B";
        ElapsedText = "";
        IsScanning = true;
        Status = "Scanning...";
        _cts = new CancellationTokenSource();

        var sw = Stopwatch.StartNew();
        var progress = new Progress<ScanProgress>(p =>
        {
            CurrentPath = p.CurrentPath;
            TotalItems = p.ItemsFound;
            TotalBytes = p.BytesFound;
            FoundSizeText = BytesToHuman(p.BytesFound);
            ElapsedText = $"{sw.Elapsed.TotalSeconds:0.0}s";
        });

        int found = 0;
        try
        {
            await Task.Run(async () =>
            {
                await foreach (var item in _scan.ScanAsync(SelectedPreset, progress, _cts.Token))
                {
                    found++;
                    Items.Add(item);
                }
            });

            Debug.WriteLine($"[ScanVM] Done: {found} items, {TotalBytes} bytes");
            Status = found > 0
                ? $"Done. {found} items, {BytesToHuman(TotalBytes)}"
                : $"Done. No cleanable files found. (check logs: %LocalAppData%/WinVitals/logs/)";
            CurrentPath = "";
            ElapsedText = $"Done in {sw.Elapsed.TotalSeconds:0.0}s";
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[ScanVM] Cancelled after {found} items");
            Status = "Cancelled";
            CurrentPath = "";
            ElapsedText = "Stopped";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScanVM] ERROR: {ex}");
            Status = "Error: " + ex.Message;
            CurrentPath = "";
            ElapsedText = "Failed";
        }
        finally { IsScanning = false; }
    }

    /// <summary>Public method for code-behind direct call.</summary>
    public void CancelScan()
    {
        Debug.WriteLine("[ScanVM] CancelScan() called");
        _cts?.Cancel();
    }

    private static string BytesToHuman(long b)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = b;
        int u = 0;
        while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
        return $"{size:0.##} {units[u]}";
    }
}
