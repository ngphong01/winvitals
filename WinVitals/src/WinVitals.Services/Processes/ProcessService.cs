using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WinVitals.Core.Entities;
using WinVitals.Core.Probes;

namespace WinVitals.Services.Processes;

public sealed class ProcessService : IProcessService
{
    private readonly ILogger<ProcessService> _log;
    private readonly ProcessProbe _probe = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ProcessService(ILogger<ProcessService> log) => _log = log;

    public async Task<IReadOnlyList<ProcessSnapshot>> RefreshAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await Task.Run(() => _probe.Snapshot(), ct);
        }
        finally { _lock.Release(); }
    }

    public Task<bool> EndProcessAsync(int pid)
    {
        return Task.Run(() =>
        {
            try
            {
                ProcessProbe.Kill(pid);
                _log.LogInformation("Killed pid {Pid}", pid);
                return true;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Kill pid {Pid} failed", pid);
                return false;
            }
        });
    }

    public Task<bool> OpenLocationAsync(int pid)
    {
        return Task.Run(() =>
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                var path = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
                return true;
            }
            catch { return false; }
        });
    }
}
