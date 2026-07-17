namespace WinVitals.Core.Telemetry;

public sealed class NullTelemetry : ITelemetry
{
    public bool IsEnabled { get; set; }
    public void TrackEvent(string name, IReadOnlyDictionary<string, object>? props = null) { }
    public void TrackException(Exception ex, IReadOnlyDictionary<string, object>? props = null) { }
    public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
}
