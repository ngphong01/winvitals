namespace WinVitals.Core.Telemetry;

public interface ITelemetry
{
    bool IsEnabled { get; set; }
    void TrackEvent(string name, IReadOnlyDictionary<string, object>? props = null);
    void TrackException(Exception ex, IReadOnlyDictionary<string, object>? props = null);
    Task FlushAsync(CancellationToken ct = default);
}
