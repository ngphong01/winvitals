using System.Text.Json;
using App.Core;

namespace AppUI;

/// <summary>
/// CLI runner for Windows Health Manager.
/// Supports scan, clean, report, and schedule commands.
/// Requirements: 15.1-15.10
/// </summary>
public class CliRunner
{
    private readonly IScannerService _scanner;
    private readonly ICleanerService _cleaner;
    private readonly IPerformanceService _performance;

    public CliRunner(IScannerService scanner, ICleanerService cleaner, IPerformanceService performance)
    {
        _scanner = scanner;
        _cleaner = cleaner;
        _performance = performance;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "scan" => await ScanAsync(args[1..]),
            "clean" => await CleanAsync(args[1..]),
            "report" => await ReportAsync(args[1..]),
            _ => UnknownCommand(args[0])
        };
    }

    private async Task<int> ScanAsync(string[] args)
    {
        var drive = GetArg(args, "--drive", "-d") ?? "C:";
        var typeStr = GetArg(args, "--type", "-t") ?? "quick";
        var type = typeStr.ToLowerInvariant() switch
        {
            "deep" => ScanType.Deep,
            "dev" or "developer" => ScanType.Developer,
            _ => ScanType.Quick
        };

        Console.WriteLine($"Scanning {drive} ({type})...");

        var options = new ScanOptions { Drives = [drive], Type = type };
        var progress = new Progress<(string Status, int Progress)>(p =>
            Console.WriteLine($"  [{p.Progress}%] {p.Status}"));

        var results = await _scanner.ScanAsync(options, progress);

        Console.WriteLine($"\nFound {results.Count} items ({ScanItem.FormatSize(results.Sum(r => r.SizeBytes))})");
        foreach (var item in results.OrderByDescending(r => r.SizeBytes).Take(20))
            Console.WriteLine($"  {item.SizeFormatted,10}  [{item.Category}] {item.Name}");

        return 0;
    }

    private async Task<int> CleanAsync(string[] args)
    {
        var levelStr = GetArg(args, "--level", "-l") ?? "quick";
        var preview = HasFlag(args, "--preview", "-p");

        var level = levelStr.ToLowerInvariant() switch
        {
            "deep" => CleanLevel.Deep,
            "dev" => CleanLevel.Developer,
            _ => CleanLevel.Quick
        };

        var options = new CleanOptions { Level = level, PreviewOnly = preview };

        if (preview)
            Console.WriteLine($"Preview mode ({level}) — no files will be deleted.");
        else
            Console.WriteLine($"Cleaning ({level})...");

        // Run a quick scan first
        var scanOptions = new ScanOptions { Drives = ["C:"], Type = ScanType.Quick };
        var items = await _scanner.ScanAsync(scanOptions);

        var previewResults = await _cleaner.PreviewAsync(items, options);
        Console.WriteLine($"\n{previewResults.Count} items evaluated:");

        foreach (var item in previewResults.OrderByDescending(r => r.SizeBytes).Take(20))
            Console.WriteLine($"  [{item.RecommendedAction}] {item.SizeFormatted,10}  {item.Name}  — {item.Suggestion}");

        if (!preview)
        {
            var (freed, processed, blocked, errors) =
                await _cleaner.CleanAsync(previewResults, options,
                    new Progress<string>(msg => Console.WriteLine($"  {msg}")));

            Console.WriteLine($"\nDone: {ScanItem.FormatSize(freed)} freed, {processed} processed, {blocked} blocked");
            if (errors.Count > 0)
                foreach (var err in errors) Console.WriteLine($"  Error: {err}");
        }

        return 0;
    }

    private async Task<int> ReportAsync(string[] args)
    {
        var format = GetArg(args, "--format", "-f") ?? "json";
        var perf = await _performance.CaptureSnapshotAsync();

        if (format == "json")
        {
            var json = JsonSerializer.Serialize(perf, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"CPU: {perf.CpuPercent:F1}%");
            Console.WriteLine($"Memory: {perf.MemoryPercent:F1}% ({perf.MemoryUsedGB:F1}/{perf.MemoryTotalGB:F1} GB)");
            Console.WriteLine($"Disk: {perf.DiskPercent:F1}% ({perf.DriveLetter}:)");
        }

        return 0;
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}");
        Console.Error.WriteLine("Run 'whm help' for usage.");
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Windows Health Manager CLI — tác giả: Đào Văn Phong

Usage: whm <command> [options]

Commands:
  scan    -d C: -t quick|deep|dev   Scan for cleanable items
  clean   -l quick|deep|dev -p      Clean or preview (with -p)
  report  -f json|text              Show system status

Examples:
  whm scan -d D: -t deep
  whm clean -l quick -p
  whm report -f json
");
    }

    private static string? GetArg(string[] args, string flag, string shortFlag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals(shortFlag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static bool HasFlag(string[] args, string flag, string shortFlag)
        => args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase) ||
                         a.Equals(shortFlag, StringComparison.OrdinalIgnoreCase));
}
