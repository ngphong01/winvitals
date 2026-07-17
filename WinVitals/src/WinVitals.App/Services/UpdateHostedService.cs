using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WinVitals.App.Services;

public sealed class UpdateHostedService : BackgroundService
{
    private readonly IUpdateService _updates;
    private readonly IAppNotifier _notify;
    private readonly ILocalizationService _loc;
    private readonly ILogger<UpdateHostedService> _log;

    public event EventHandler<UpdateStatus>? UpdateFound;

    public UpdateHostedService(
        IUpdateService updates, IAppNotifier notify,
        ILocalizationService loc, ILogger<UpdateHostedService> log)
    {
        _updates = updates; _notify = notify; _loc = loc; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _updates.CheckAsync(ct);
                if (status.HasUpdate)
                {
                    _log.LogInformation("Update {Version} available", status.LatestVersion);
                    UpdateFound?.Invoke(this, status);
                    _notify.Info(
                        _loc["Update_AvailableTitle"],
                        string.Format(_loc["Update_AvailableFmt"], status.LatestVersion));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogWarning(ex, "Update check iteration failed"); }

            try { await Task.Delay(TimeSpan.FromHours(6), ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
