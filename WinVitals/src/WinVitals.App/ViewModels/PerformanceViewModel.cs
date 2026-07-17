using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WinVitals.Services.Metrics;

namespace WinVitals.App.ViewModels;

public sealed partial class PerformanceViewModel : ObservableObject, IDisposable
{
    private const int MaxPoints = 60;
    private readonly IMetricsService _metrics;
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<DateTimePoint> CpuPoints { get; } = new();
    public ObservableCollection<DateTimePoint> RamPoints { get; } = new();

    public ISeries[] Series { get; }

    public Axis[] XAxes { get; } =
    {
        new Axis
        {
            Labeler = v => SafeDateTimeLabel((long)v, "HH:mm:ss"),
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            UnitWidth = TimeSpan.FromSeconds(1).Ticks,
            MinStep  = TimeSpan.FromSeconds(5).Ticks,
        }
    };

    public Axis[] YAxes { get; } =
    {
        new Axis { MinLimit = 0, MaxLimit = 100,
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
            Labeler = v => $"{v:0}%" }
    };

    [ObservableProperty] private double cpuNow;
    [ObservableProperty] private double ramNow;
    [ObservableProperty] private double ramUsedGb;
    [ObservableProperty] private double ramTotalGb;
    [ObservableProperty] private int healthScore = 100;

    public ProcessesViewModel Processes { get; }

    public PerformanceViewModel(IMetricsService metrics, ProcessesViewModel processes)
    {
        _metrics = metrics;
        Processes = processes;

        Series = new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Name = "CPU",
                Values = CpuPoints,
                GeometrySize = 0,
                LineSmoothness = 0.5,
                Stroke = new SolidColorPaint(SKColor.Parse("#60A5FA"), 2),
                Fill  = new SolidColorPaint(SKColor.Parse("#3060A5FA")),
            },
            new LineSeries<DateTimePoint>
            {
                Name = "RAM",
                Values = RamPoints,
                GeometrySize = 0,
                LineSmoothness = 0.5,
                Stroke = new SolidColorPaint(SKColor.Parse("#A78BFA"), 2),
                Fill  = new SolidColorPaint(SKColor.Parse("#30A78BFA")),
            }
        };

        _ = Task.Run(ConsumeAsync);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var s in _metrics.Stream.ReadAllAsync(_cts.Token))
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    CpuNow = s.CpuPercent;
                    RamNow = s.RamPercent;
                    RamUsedGb = s.RamUsedGb;
                    RamTotalGb = s.RamTotalGb;
                    HealthScore = s.HealthScore;

                    CpuPoints.Add(new DateTimePoint(s.TimestampUtc.ToLocalTime(), s.CpuPercent));
                    RamPoints.Add(new DateTimePoint(s.TimestampUtc.ToLocalTime(), s.RamPercent));
                    while (CpuPoints.Count > MaxPoints) CpuPoints.RemoveAt(0);
                    while (RamPoints.Count > MaxPoints) RamPoints.RemoveAt(0);
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => _cts.Cancel();

    private static string SafeDateTimeLabel(long ticks, string format)
    {
        if (ticks <= 0 || ticks >= DateTime.MaxValue.Ticks) return "";
        try { return new DateTime(ticks).ToString(format); }
        catch { return ""; }
    }
}
