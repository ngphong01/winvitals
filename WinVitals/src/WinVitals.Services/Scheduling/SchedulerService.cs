using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;
using WinVitals.Services.Cleaning;
using WinVitals.Services.Scanning;

namespace WinVitals.Services.Scheduling;

public sealed class SchedulerService : BackgroundService, ISchedulerService
{
    private readonly IScanService _scan;
    private readonly ICleanService _clean;
    private readonly SettingsStore _settings;
    private readonly ILogger<SchedulerService> _log;

    public event EventHandler<ScheduleRunEventArgs>? RunCompleted;
    public DateTime? NextRunUtc { get; private set; }

    public SchedulerService(
        IScanService scan, ICleanService clean, SettingsStore settings,
        ILogger<SchedulerService> log)
    {
        _scan = scan;
        _clean = clean;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var s = _settings.Get();
            NextRunUtc = ComputeNextRun(s, DateTime.UtcNow);

            if (NextRunUtc is null || s.ScheduleFrequency == ScheduleFrequency.Never)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            var wait = NextRunUtc.Value - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                _log.LogInformation("Next scheduled run at {Time} (in {Wait})", NextRunUtc, wait);
                try { await Task.Delay(wait, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            try
            {
                await RunOnceAsync(s, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Scheduled run failed");
            }
        }
    }

    public Task TriggerNowAsync(CancellationToken ct = default) =>
        RunOnceAsync(_settings.Get(), ct);

    private async Task RunOnceAsync(AppSettings s, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _log.LogInformation("Scheduled run: preset={Preset}, auto={Auto}", s.SchedulePreset, s.ScheduleAutoConfirm);

        var items = new List<Core.Entities.ScanItem>();
        await foreach (var it in _scan.ScanAsync(s.SchedulePreset, null, ct))
            items.Add(it);

        Core.Entities.CleanReport report;
        if (s.ScheduleAutoConfirm)
            report = await _clean.ExecuteAsync(items, null, ct, s.SchedulePreset, wasScheduled: true);
        else
        {
            report = await _clean.PreviewAsync(items, null, ct);
            _log.LogInformation("Preview-only: would clean {N} items", report.Quarantined);
        }

        sw.Stop();
        s.LastScheduledRunUtc = DateTime.UtcNow;
        _settings.Save(s);

        RunCompleted?.Invoke(this, new ScheduleRunEventArgs(report.Quarantined, report.BytesFreed, sw.Elapsed));
    }

    public static DateTime? ComputeNextRun(AppSettings s, DateTime nowUtc)
    {
        if (s.ScheduleFrequency == ScheduleFrequency.Never) return null;

        var localNow = nowUtc.ToLocalTime();
        var todayAt = new DateTime(localNow.Year, localNow.Month, localNow.Day,
            s.ScheduleTime.Hour, s.ScheduleTime.Minute, 0, DateTimeKind.Local);

        DateTime next = s.ScheduleFrequency switch
        {
            ScheduleFrequency.Daily => todayAt > localNow ? todayAt : todayAt.AddDays(1),
            ScheduleFrequency.Weekly => NextWeekday(todayAt, s.ScheduleDayOfWeek, localNow),
            ScheduleFrequency.Monthly => NextMonthly(localNow, s.ScheduleTime, s.ScheduleDayOfMonth),
            _ => localNow.AddYears(100)
        };

        return next.ToUniversalTime();
    }

    private static DateTime NextWeekday(DateTime todayAt, DayOfWeek target, DateTime now)
    {
        int daysAhead = ((int)target - (int)now.DayOfWeek + 7) % 7;
        if (daysAhead == 0 && todayAt <= now) daysAhead = 7;
        return todayAt.AddDays(daysAhead);
    }

    private static DateTime NextMonthly(DateTime now, TimeOnly time, int day)
    {
        int safeDay = Math.Clamp(day, 1, 28);
        var thisMonth = new DateTime(now.Year, now.Month, safeDay, time.Hour, time.Minute, 0, DateTimeKind.Local);
        return thisMonth > now ? thisMonth : thisMonth.AddMonths(1);
    }
}
