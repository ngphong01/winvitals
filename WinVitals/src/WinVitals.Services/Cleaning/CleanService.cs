using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;
using WinVitals.Core.Storage;
using WinVitals.Services.Quarantine;

namespace WinVitals.Services.Cleaning;

public sealed class CleanService : ICleanService
{
    private readonly IQuarantineService _quarantine;
    private readonly RuleRepository _rules;
    private readonly CleanSessionStore _sessions;
    private readonly SettingsStore _settings;
    private readonly ILogger<CleanService> _log;

    public event EventHandler<CleanSession>? SessionCompleted;

    public CleanService(
        IQuarantineService quarantine,
        RuleRepository rules,
        CleanSessionStore sessions,
        SettingsStore settings,
        ILogger<CleanService> log)
    {
        _quarantine = quarantine;
        _rules = rules;
        _sessions = sessions;
        _settings = settings;
        _log = log;
        _rules.Changed += (_, _) => { lock (_evalLock) _evaluator = null; };
    }

    private RuleEvaluator? _evaluator;
    private readonly object _evalLock = new();

    private RuleEvaluator Evaluator
    {
        get
        {
            lock (_evalLock)
                return _evaluator ??= new RuleEvaluator(_rules.LoadAll());
        }
    }

    public Task<CleanReport> PreviewAsync(
        IEnumerable<ScanItem> items,
        IProgress<CleanProgress>? progress,
        CancellationToken ct)
        => RunAsync(items, progress, ct, dryRun: true);

    public Task<CleanReport> ExecuteAsync(
        IEnumerable<ScanItem> items,
        IProgress<CleanProgress>? progress,
        CancellationToken ct)
        => ExecuteAsync(items, progress, ct, ScanPreset.Quick, wasScheduled: false);

    public async Task<CleanReport> ExecuteAsync(
        IEnumerable<ScanItem> items,
        IProgress<CleanProgress>? progress,
        CancellationToken ct,
        ScanPreset preset,
        bool wasScheduled)
    {
        var startedAt = DateTime.UtcNow;
        var list = items.ToList();
        var report = await RunAsync(list, progress, ct, dryRun: false);
        var completedAt = DateTime.UtcNow;

        var session = new CleanSession
        {
            StartedAtUtc = startedAt,
            CompletedAtUtc = completedAt,
            Preset = preset,
            WasScheduled = wasScheduled,
            TotalItems = report.TotalRequested,
            QuarantinedCount = report.Quarantined,
            SkippedCount = report.Skipped,
            FailedCount = report.Failed,
            BytesFreed = report.BytesFreed
        };

        foreach (var outcome in report.Outcomes.Where(o => o.Status == CleanStatus.Quarantined))
        {
            var item = list.FirstOrDefault(i => i.Path == outcome.Path);
            if (item is null) continue;
            var cat = item.Category;
            session.BytesByCategory[cat] = session.BytesByCategory.GetValueOrDefault(cat) + outcome.SizeBytes;
            session.CountByCategory[cat] = session.CountByCategory.GetValueOrDefault(cat) + 1;
        }

        try { _sessions.Save(session); }
        catch (Exception ex) { _log.LogWarning(ex, "Failed to persist clean session"); }

        SessionCompleted?.Invoke(this, session);
        return report;
    }

    private async Task<CleanReport> RunAsync(
        IEnumerable<ScanItem> items,
        IProgress<CleanProgress>? progress,
        CancellationToken ct,
        bool dryRun)
    {
        var list = items.ToList();
        var outcomes = new List<CleanOutcome>(list.Count);
        var sw = Stopwatch.StartNew();
        long bytesFreed = 0;
        int processed = 0;

        // Reload rules – user có thể vừa sửa
        var evaluator = Evaluator;
        var exclusions = new ExclusionFilter(_settings.Get().ExcludedPatterns);

        foreach (var item in list)
        {
            ct.ThrowIfCancellationRequested();
            processed++;

            var outcome = await ProcessOneAsync(item, dryRun, evaluator, exclusions, ct);
            outcomes.Add(outcome);
            if (outcome.Status == CleanStatus.Quarantined)
                bytesFreed += outcome.SizeBytes;

            if (processed % 5 == 0 || processed == list.Count)
            {
                progress?.Report(new CleanProgress(item.Path, processed, list.Count, bytesFreed));
            }
        }

        sw.Stop();

        var report = new CleanReport(
            TotalRequested: list.Count,
            Quarantined: outcomes.Count(o => o.Status == CleanStatus.Quarantined),
            Skipped: outcomes.Count(o => o.Status is CleanStatus.Skipped_Blocked
                                              or CleanStatus.Skipped_Missing
                                              or CleanStatus.Skipped_UserChoice),
            Failed: outcomes.Count(o => o.Status == CleanStatus.Failed),
            BytesFreed: bytesFreed,
            Elapsed: sw.Elapsed,
            Outcomes: outcomes);

        _log.LogInformation(
            "Clean {Mode} done: {Q} quarantined, {S} skipped, {F} failed, {MB:0.0} MB freed in {Ms} ms",
            dryRun ? "PREVIEW" : "EXEC",
            report.Quarantined, report.Skipped, report.Failed,
            report.BytesFreed / 1024.0 / 1024.0, sw.ElapsedMilliseconds);

        return report;
    }

    private async Task<CleanOutcome> ProcessOneAsync(ScanItem item, bool dryRun,
        RuleEvaluator evaluator, ExclusionFilter exclusions, CancellationToken ct)
    {
        // Idempotent guards
        if (!File.Exists(item.Path))
            return new CleanOutcome(item.Path, item.SizeBytes, CleanStatus.Skipped_Missing);

        if (exclusions.IsExcluded(item.Path))
            return new CleanOutcome(item.Path, item.SizeBytes, CleanStatus.Skipped_UserChoice,
                "Excluded by user setting");

        // Re-evaluate: rule có thể đã đổi kể từ lúc scan
        long size; DateTime lastMod;
        try
        {
            var fi = new FileInfo(item.Path);
            size = fi.Length;
            lastMod = fi.LastWriteTimeUtc;
        }
        catch
        {
            return new CleanOutcome(item.Path, item.SizeBytes, CleanStatus.Skipped_Missing);
        }

        var match = evaluator.Evaluate(item.Path, size, lastMod);

        if (match.Action == ItemAction.Block)
        {
            _log.LogWarning("Blocked by rule {RuleId}: {Path}", match.RuleId, item.Path);
            return new CleanOutcome(item.Path, size, CleanStatus.Skipped_Blocked,
                ErrorMessage: $"Blocked by rule '{match.RuleName}'");
        }

        if (dryRun)
        {
            return new CleanOutcome(item.Path, size, CleanStatus.Quarantined);
        }

        try
        {
            var entry = await _quarantine.QuarantineAsync(
                item.Path,
                reason: $"Rule={match.RuleId} Category={item.Category}",
                risk: match.Risk,
                retention: null,
                ct: ct);

            if (entry is null)
                return new CleanOutcome(item.Path, size, CleanStatus.Skipped_Missing);

            return new CleanOutcome(item.Path, size, CleanStatus.Quarantined, QuarantineId: entry.Id);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clean failed for {Path}", item.Path);
            return new CleanOutcome(item.Path, size, CleanStatus.Failed, ErrorMessage: ex.Message);
        }
    }
}
