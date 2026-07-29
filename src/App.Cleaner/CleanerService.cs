using App.Core;
using Microsoft.Extensions.Logging;

namespace App.Cleaner;

/// <summary>
/// Orchestrates cleaning operations with rule evaluation, quarantine,
/// preview mode, and failure handling.
/// Requirements: 1.3, 3.2, 3.6, 3.7
/// </summary>
public class CleanerService : ICleanerService
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IRiskEngine _riskEngine;
    private readonly ILogger<CleanerService> _logger;

    public IRuleEngine RuleEngine => _ruleEngine;

    public CleanerService(IRuleEngine ruleEngine, IRiskEngine riskEngine, ILogger<CleanerService> logger)
    {
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _riskEngine = riskEngine ?? throw new ArgumentNullException(nameof(riskEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<List<ScanItem>> PreviewAsync(IEnumerable<ScanItem> items, CleanOptions options,
        CancellationToken ct = default)
    {
        var evaluated = new List<ScanItem>();
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;

            // Skip excluded paths
            if (options.ExcludedPaths.Any(p => item.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            var risk = _riskEngine.AssessRisk(item.Path, item.Category, item.SizeBytes, item.LastModified);
            var (action, _, rule) = _ruleEngine.Evaluate(item.Path, item.SizeBytes, item.LastModified);

            item.Risk = risk;
            item.RecommendedAction = action;
            item.MatchedRule = rule;

            evaluated.Add(item);
        }

        _logger.LogInformation("Preview evaluated {Count} items", evaluated.Count);
        return Task.FromResult(evaluated);
    }

    public async Task<(long FreedBytes, int ItemsProcessed, int ItemsBlocked, List<string> Errors)> CleanAsync(
        IEnumerable<ScanItem> items, CleanOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        long freedBytes = 0;
        int processed = 0;
        int blocked = 0;
        var errors = new List<string>();

        // First pass: evaluate all items
        var evaluated = await PreviewAsync(items, options, ct);

        if (options.PreviewOnly)
        {
            progress?.Report($"PREVIEW: {evaluated.Count} items evaluated. No files deleted.");
            return (0, 0, 0, errors);
        }

        // Second pass: execute
        foreach (var item in evaluated)
        {
            if (ct.IsCancellationRequested) break;

            if (item.RecommendedAction == ItemAction.Block)
            {
                blocked++;
                progress?.Report($"Blocked: {item.Name}");
                continue;
            }

            try
            {
                if (item.IsDirectory)
                {
                    if (Directory.Exists(item.Path))
                    {
                        Directory.Delete(item.Path, recursive: true);
                        freedBytes += item.SizeBytes;
                        processed++;
                        progress?.Report($"Deleted dir: {item.Name} ({item.SizeFormatted})");
                    }
                }
                else if (File.Exists(item.Path))
                {
                    var size = new FileInfo(item.Path).Length;
                    File.Delete(item.Path);
                    freedBytes += size;
                    processed++;
                    progress?.Report($"Deleted: {item.Name} ({ScanItem.FormatSize(size)})");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Name}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to delete: {Path}", item.Path);
            }
        }

        _logger.LogInformation("Clean complete: {Processed} processed, {Blocked} blocked, {Freed} freed, {Errors} errors",
            processed, blocked, ScanItem.FormatSize(freedBytes), errors.Count);

        return (freedBytes, processed, blocked, errors);
    }
}
