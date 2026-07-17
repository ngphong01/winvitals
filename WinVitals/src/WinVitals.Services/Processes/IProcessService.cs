using WinVitals.Core.Entities;

namespace WinVitals.Services.Processes;

public interface IProcessService
{
    Task<IReadOnlyList<ProcessSnapshot>> RefreshAsync(CancellationToken ct = default);
    Task<bool> EndProcessAsync(int pid);
    Task<bool> OpenLocationAsync(int pid);
}
