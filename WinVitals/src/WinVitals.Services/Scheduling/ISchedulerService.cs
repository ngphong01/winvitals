namespace WinVitals.Services.Scheduling;

public interface ISchedulerService
{
    DateTime? NextRunUtc { get; }
    Task TriggerNowAsync(CancellationToken ct = default);
    event EventHandler<ScheduleRunEventArgs>? RunCompleted;
}

public sealed record ScheduleRunEventArgs(int ItemsCleaned, long BytesFreed, TimeSpan Elapsed);
