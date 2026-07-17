using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WinVitals.Core.Analytics;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;
using WinVitals.Services.Cleaning;
using WinVitals.Services.Quarantine;

namespace WinVitals.App.ViewModels;

public sealed partial class StatisticsViewModel : ObservableObject
{
    private readonly CleanSessionStore _sessions;
    private readonly IQuarantineService _quarantine;

    [ObservableProperty] private long totalBytesEver;
    [ObservableProperty] private int totalSessionsEver;
    [ObservableProperty] private long quarantineBytesNow;
    [ObservableProperty] private int currentStreak;
    [ObservableProperty] private long bytesThisMonth;
    [ObservableProperty] private long bytesLastMonth;
    [ObservableProperty] private double monthOverMonthPercent;
    [ObservableProperty] private string monthTrendText = "";
    [ObservableProperty] private RangeChoice selectedRange = RangeChoice.Last30Days;

    public ObservableCollection<ISeries> BytesTrendSeries { get; } = new();
    public ObservableCollection<Axis> TrendXAxes { get; } = new();
    public ObservableCollection<Axis> TrendYAxes { get; } = new();
    public ObservableCollection<ISeries> DayOfWeekSeries { get; } = new();
    public ObservableCollection<Axis> DayXAxes { get; } = new();
    public ObservableCollection<Axis> DayYAxes { get; } = new();
    public ObservableCollection<ISeries> CategorySeries { get; } = new();
    public ObservableCollection<CleanSession> RecentSessions { get; } = new();
    public ObservableCollection<InsightCard> Insights { get; } = new();
    public RangeChoice[] AllRanges { get; } = Enum.GetValues<RangeChoice>();

    public StatisticsViewModel(CleanSessionStore sessions, IQuarantineService quarantine, ICleanService clean)
    {
        _sessions = sessions;
        _quarantine = quarantine;
        SetupAxes();
        Refresh();
        clean.SessionCompleted += (_, _) => App.Current.Dispatcher.BeginInvoke(() => Refresh());
    }

    partial void OnSelectedRangeChanged(RangeChoice v) => Refresh();

    private void SetupAxes()
    {
        var lp = new SolidColorPaint(SKColors.LightGray);
        TrendXAxes.Add(new Axis
        {
            Labeler = v => SafeDateTimeLabel((long)v, "MMM dd"),
            LabelsPaint = lp,
            UnitWidth = TimeSpan.FromDays(1).Ticks,
            MinStep = TimeSpan.FromDays(1).Ticks,
            LabelsRotation = 30
        });
        TrendYAxes.Add(new Axis { Labeler = v => FormatBytes((long)v), LabelsPaint = lp, MinLimit = 0 });
        DayXAxes.Add(new Axis { Labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, LabelsPaint = lp });
        DayYAxes.Add(new Axis { LabelsPaint = lp, MinLimit = 0 });
    }

    [RelayCommand]
    private void Refresh()
    {
        var now = DateTime.UtcNow;
        var (from, to) = ResolveRange(SelectedRange, now);
        var sessions = _sessions.Between(from, to);
        var allTime = _sessions.Recent(1000);

        TotalBytesEver = _sessions.TotalBytesEver();
        TotalSessionsEver = _sessions.TotalSessions();
        QuarantineBytesNow = _quarantine.TotalQuarantinedBytes;
        CurrentStreak = StreakCalculator.Compute(
            allTime.Select(s => s.CompletedAtUtc.ToLocalTime()), now.ToLocalTime());

        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        BytesThisMonth = allTime.Where(s => s.CompletedAtUtc >= thisMonth).Sum(s => s.BytesFreed);
        BytesLastMonth = allTime.Where(s => s.CompletedAtUtc >= lastMonth && s.CompletedAtUtc < thisMonth).Sum(s => s.BytesFreed);
        if (BytesLastMonth > 0)
        {
            MonthOverMonthPercent = Math.Round((BytesThisMonth - BytesLastMonth) / (double)BytesLastMonth * 100, 1);
            MonthTrendText = MonthOverMonthPercent >= 0 ? $"↑ +{MonthOverMonthPercent:0.#}% vs last month" : $"↓ {MonthOverMonthPercent:0.#}% vs last month";
        }
        else { MonthOverMonthPercent = 0; MonthTrendText = BytesThisMonth > 0 ? "First month" : "No data"; }

        BuildBytesTrend(sessions, from, to);
        BuildDayOfWeek(sessions);
        BuildCategoryPie(sessions);
        BuildRecent(allTime);
        BuildInsights(sessions, allTime, now);
    }

    private void BuildBytesTrend(IReadOnlyList<CleanSession> sessions, DateTime from, DateTime to)
    {
        var byDay = sessions.GroupBy(s => s.CompletedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.BytesFreed));
        var points = new List<DateTimePoint>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            byDay.TryGetValue(d, out var b);
            points.Add(new DateTimePoint(d.ToLocalTime(), b));
        }
        BytesTrendSeries.Clear();
        BytesTrendSeries.Add(new ColumnSeries<DateTimePoint>
        {
            Name = "Bytes",
            Values = points,
            Fill = new SolidColorPaint(SKColor.Parse("#89B4FA")),
            Stroke = null,
            MaxBarWidth = 20,
            Padding = 2
        });
    }

    private void BuildDayOfWeek(IReadOnlyList<CleanSession> sessions)
    {
        var counts = new int[7];
        foreach (var s in sessions)
        {
            var dow = (int)s.CompletedAtUtc.ToLocalTime().DayOfWeek;
            counts[dow == 0 ? 6 : dow - 1]++;
        }
        DayOfWeekSeries.Clear();
        DayOfWeekSeries.Add(new ColumnSeries<int>
        {
            Name = "Sessions",
            Values = counts.ToList(),
            Fill = new SolidColorPaint(SKColor.Parse("#A6E3A1")),
            Stroke = null,
            MaxBarWidth = 32
        });
    }

    private void BuildCategoryPie(IReadOnlyList<CleanSession> sessions)
    {
        var agg = new Dictionary<ItemCategory, long>();
        foreach (var s in sessions)
            foreach (var kv in s.BytesByCategory)
                agg[kv.Key] = agg.GetValueOrDefault(kv.Key) + kv.Value;
        var palette = new[] { "#89B4FA", "#A6E3A1", "#F9E2AF", "#FAB387", "#F38BA8", "#CBA6F7", "#94E2D5", "#EBA0AC", "#89DCEB", "#B4BEFE" };
        CategorySeries.Clear();
        int i = 0;
        foreach (var (cat, bytes) in agg.OrderByDescending(kv => kv.Value))
        {
            CategorySeries.Add(new PieSeries<long>
            {
                Name = cat.ToString(),
                Values = new[] { bytes },
                Fill = new SolidColorPaint(SKColor.Parse(palette[i % palette.Length])),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 12,
                InnerRadius = 60,
                DataLabelsFormatter = p => FormatBytes((long)p.Coordinate.PrimaryValue)
            });
            i++;
        }
        if (CategorySeries.Count == 0)
            CategorySeries.Add(new PieSeries<long>
            {
                Name = "No data",
                Values = new[] { 1L },
                Fill = new SolidColorPaint(SKColor.Parse("#404050")),
                InnerRadius = 60
            });
    }

    private void BuildRecent(IReadOnlyList<CleanSession> all)
    {
        RecentSessions.Clear();
        foreach (var s in all.OrderByDescending(x => x.CompletedAtUtc).Take(20))
            RecentSessions.Add(s);
    }

    private void BuildInsights(IReadOnlyList<CleanSession> ranged, IReadOnlyList<CleanSession> all, DateTime now)
    {
        Insights.Clear();
        if (ranged.Count == 0) { Insights.Add(new InsightCard("No activity", "Run a Quick clean to see stats.", InsightKind.Info)); return; }

        var biggest = ranged.MaxBy(s => s.BytesFreed);
        if (biggest is not null && biggest.BytesFreed > 0)
            Insights.Add(new InsightCard("Biggest cleanup", $"On {biggest.CompletedAtUtc.ToLocalTime():MMM dd}, reclaimed {FormatBytes(biggest.BytesFreed)}.", InsightKind.Success));

        var catAgg = ranged.SelectMany(s => s.BytesByCategory).GroupBy(kv => kv.Key)
            .Select(g => new { Cat = g.Key, Bytes = g.Sum(kv => kv.Value) }).MaxBy(x => x.Bytes);
        if (catAgg is not null && catAgg.Bytes > 0)
        {
            var total = ranged.Sum(s => s.BytesFreed);
            var pct = total > 0 ? catAgg.Bytes * 100.0 / total : 0;
            Insights.Add(new InsightCard("Top category", $"{catAgg.Cat} accounts for {pct:0}% ({FormatBytes(catAgg.Bytes)}).", InsightKind.Info));
        }

        var scheduled = ranged.Count(s => s.WasScheduled);
        if (scheduled == 0 && ranged.Count >= 3)
            Insights.Add(new InsightCard("Tip: automate", "Enable scheduling in Settings for automatic cleanup.", InsightKind.Tip));

        if (CurrentStreak >= 3)
            Insights.Add(new InsightCard($"{CurrentStreak}-day streak", "Regular cleanups keep your system fast.", InsightKind.Success));

        var expiring = _quarantine.GetActive().Count(e => (e.ExpiresAtUtc - now).TotalDays <= 3);
        if (expiring > 0)
            Insights.Add(new InsightCard("Expiring soon", $"{expiring} item(s) will be purged within 3 days.", InsightKind.Warning));
    }

    private static (DateTime f, DateTime t) ResolveRange(RangeChoice r, DateTime now)
    {
        var today = now.Date;
        return r switch
        {
            RangeChoice.Last7Days => (today.AddDays(-6), now),
            RangeChoice.Last30Days => (today.AddDays(-29), now),
            RangeChoice.Last90Days => (today.AddDays(-89), now),
            RangeChoice.ThisYear => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now),
            _ => (DateTime.MinValue.ToUniversalTime(), now)
        };
    }

    private static string FormatBytes(long b)
    {
        if (b <= 0) return "0 B";
        string[] u = { "B", "KB", "MB", "GB", "TB" }; double s = b; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.##} {u[i]}";
    }

    private static string SafeDateTimeLabel(long ticks, string format)
    {
        if (ticks <= 0 || ticks >= DateTime.MaxValue.Ticks) return "";
        try { return new DateTime(ticks).ToString(format); }
        catch { return ""; }
    }
}

public enum RangeChoice { Last7Days, Last30Days, Last90Days, ThisYear, AllTime }
public enum InsightKind { Info, Success, Warning, Tip }
public sealed record InsightCard(string Title, string Description, InsightKind Kind)
{
    public string Icon => Kind switch { InsightKind.Success => "✓", InsightKind.Warning => "⚠", InsightKind.Tip => "💡", _ => "ⓘ" };
}
