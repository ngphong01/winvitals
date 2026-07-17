using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace WinVitals.App.Services;

public interface IUpdateService
{
    Task<UpdateStatus> CheckAsync(CancellationToken ct = default);
    Task<bool> DownloadAsync(IProgress<int>? progress, CancellationToken ct = default);
    void ApplyAndRestart();
    string CurrentVersion { get; }
}

public sealed record UpdateStatus(
    bool HasUpdate,
    string? LatestVersion,
    string? ReleaseNotes,
    long? TotalBytes);

public sealed class UpdateService : IUpdateService
{
    private readonly ILogger<UpdateService> _log;
    private readonly UpdateManager _manager;
    private UpdateInfo? _pending;

    public string CurrentVersion { get; }

    public UpdateService(ILogger<UpdateService> log)
    {
        _log = log;
        var source = new GithubSource(
            "https://github.com/yourname/winvitals",
            accessToken: null,
            prerelease: false);
        _manager = new UpdateManager(source, logger: null);
        CurrentVersion = _manager.CurrentVersion?.ToString() ?? "0.0.0";
    }

    public async Task<UpdateStatus> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_manager.IsInstalled)
            {
                _log.LogInformation("App not installed via Velopack – updates disabled");
                return new UpdateStatus(false, null, null, null);
            }
            _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (_pending is null || _pending.TargetFullRelease is null)
                return new UpdateStatus(false, null, null, null);
            var latest = _pending.TargetFullRelease.Version.ToString();
            var notes = _pending.TargetFullRelease.NotesMarkdown;
            var size = _pending.TargetFullRelease.Size;
            _log.LogInformation("Update available: {Version} ({Size} bytes)", latest, size);
            return new UpdateStatus(true, latest, notes, size);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Update check failed");
            return new UpdateStatus(false, null, null, null);
        }
    }

    public async Task<bool> DownloadAsync(IProgress<int>? progress, CancellationToken ct = default)
    {
        if (_pending is null) { _log.LogWarning("DownloadAsync called without pending update"); return false; }
        try
        {
            await _manager.DownloadUpdatesAsync(_pending, progress: p => progress?.Report(p)).ConfigureAwait(false);
            _log.LogInformation("Update downloaded");
            return true;
        }
        catch (Exception ex) { _log.LogError(ex, "Update download failed"); return false; }
    }

    public void ApplyAndRestart()
    {
        if (_pending is null) return;
        _log.LogInformation("Applying update and restarting");
        _manager.ApplyUpdatesAndRestart(_pending);
    }
}
