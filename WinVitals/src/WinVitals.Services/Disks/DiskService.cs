using WinVitals.Core.Entities;
using WinVitals.Core.Probes;

namespace WinVitals.Services.Disks;

public sealed class DiskService : IDiskService
{
    private IReadOnlyList<SmartInfo>? _smartCache;
    private DateTime _smartCachedAt;
    private readonly TimeSpan _smartTtl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public IReadOnlyList<DriveUsage> GetDrives() => DriveUsageProbe.Read();

    public async Task<IReadOnlyList<SmartInfo>> GetSmartAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_smartCache is not null && DateTime.UtcNow - _smartCachedAt < _smartTtl)
                return _smartCache;

            _smartCache = await Task.Run(SmartProbe.Read, ct);
            _smartCachedAt = DateTime.UtcNow;
            return _smartCache;
        }
        finally { _lock.Release(); }
    }
}
