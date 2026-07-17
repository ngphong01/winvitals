using WinVitals.Core;
using WinVitals.Core.Entities;

namespace WinVitals.Services.Cleaning;

public interface ICleanService
{
    Task<CleanReport> PreviewAsync(IEnumerable<ScanItem> items, IProgress<CleanProgress>? progress, CancellationToken ct);
    Task<CleanReport> ExecuteAsync(IEnumerable<ScanItem> items, IProgress<CleanProgress>? progress, CancellationToken ct);
    Task<CleanReport> ExecuteAsync(IEnumerable<ScanItem> items, IProgress<CleanProgress>? progress, CancellationToken ct, ScanPreset preset, bool wasScheduled);
    event EventHandler<CleanSession>? SessionCompleted;
}
