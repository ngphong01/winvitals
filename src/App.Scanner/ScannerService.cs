using App.Core;

namespace App.Scanner;

/// <summary>
/// Coordinates scanning operations across multiple scanner types.
/// Handles registration, parallel execution, and result aggregation.
/// Requirements: 1.3, 3.1, 3.5, 7.7
/// </summary>
public class ScannerService : IScannerService
{
    private readonly List<IScanner> _scanners;
    private readonly object _lock = new();

    public int ScannerCount
    {
        get { lock (_lock) return _scanners.Count; }
    }

    public ScannerService(IEnumerable<IScanner> scanners)
    {
        _scanners = scanners.ToList();
    }

    public void RegisterScanner(IScanner scanner)
    {
        lock (_lock) _scanners.Add(scanner);
    }

    public async Task<List<ScanItem>> ScanAsync(ScanOptions options,
        IProgress<(string Status, int Progress)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<ScanItem>();
        var scanners = GetScannersForType(options.Type);

        if (scanners.Count == 0) return results;

        int completed = 0;
        progress?.Report(($"Starting {scanners.Count} scanners...", 0));

        var tasks = scanners.Select(async scanner =>
        {
            try
            {
                var items = await scanner.ScanAsync(options.Drives, progress, ct);
                lock (results)
                {
                    results.AddRange(items);
                }
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                progress?.Report(($"Scanner '{scanner.Name}' failed: {ex.Message}", 0));
            }
            finally
            {
                Interlocked.Increment(ref completed);
                progress?.Report(($"Completed {completed}/{scanners.Count}", completed * 100 / scanners.Count));
            }
        });

        await Task.WhenAll(tasks);
        progress?.Report(($"Scan complete: {results.Count} items found.", 100));
        return results;
    }

    private List<IScanner> GetScannersForType(ScanType type)
    {
        lock (_lock)
        {
            return type switch
            {
                ScanType.Developer => _scanners.Where(s => s.ScanType == ScanType.Developer).ToList(),
                ScanType.Quick or ScanType.Deep => _scanners.Where(s => s.ScanType == type || s.ScanType == ScanType.Quick).ToList(),
                _ => _scanners.ToList()
            };
        }
    }
}
