using WinVitals.Core;
using WinVitals.Core.Entities;

namespace WinVitals.Services.Scanning;

public interface IScanService
{
    IAsyncEnumerable<ScanItem> ScanAsync(ScanPreset preset, IProgress<ScanProgress>? progress, CancellationToken ct);
}

public sealed record ScanProgress(string CurrentPath, int ItemsFound, long BytesFound);
