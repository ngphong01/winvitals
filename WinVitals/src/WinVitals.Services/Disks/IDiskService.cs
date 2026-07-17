using WinVitals.Core.Entities;

namespace WinVitals.Services.Disks;

public interface IDiskService
{
    IReadOnlyList<DriveUsage> GetDrives();
    Task<IReadOnlyList<SmartInfo>> GetSmartAsync(CancellationToken ct = default);
}
