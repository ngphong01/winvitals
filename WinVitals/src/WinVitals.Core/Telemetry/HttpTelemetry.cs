using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WinVitals.Core.Telemetry;

public sealed class HttpTelemetry : ITelemetry, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpTelemetry> _log;
    private readonly Uri _endpoint;
    private readonly string _anonymousId;
    private readonly Channel<TelemetryEvent> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;

    public bool IsEnabled { get; set; }

    public HttpTelemetry(HttpClient http, Uri endpoint, string anonymousId, ILogger<HttpTelemetry> log)
    {
        _http = http; _endpoint = endpoint; _anonymousId = anonymousId; _log = log;
        _channel = Channel.CreateBounded<TelemetryEvent>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });
        _worker = Task.Run(RunAsync);
    }

    public void TrackEvent(string name, IReadOnlyDictionary<string, object>? props = null)
    {
        if (!IsEnabled) return;
        _channel.Writer.TryWrite(new TelemetryEvent
        {
            Name = name,
            AnonymousId = _anonymousId,
            Timestamp = DateTime.UtcNow,
            Properties = props?.ToDictionary(x => x.Key, x => x.Value) ?? new()
        });
    }

    public void TrackException(Exception ex, IReadOnlyDictionary<string, object>? props = null)
    {
        if (!IsEnabled) return;
        var p = props?.ToDictionary(x => x.Key, x => x.Value) ?? new();
        p["exception.type"] = ex.GetType().Name;
        p["exception.message"] = Redact(ex.Message);
        p["exception.stackHash"] = HashStack(ex.StackTrace ?? "");
        TrackEvent("app.exception", p);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        for (int i = 0; i < 10 && _channel.Reader.Count > 0; i++)
            await Task.Delay(100, ct);
    }

    private async Task RunAsync()
    {
        var batch = new List<TelemetryEvent>(32);
        try
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                batch.Clear();
                while (_channel.Reader.TryRead(out var ev) && batch.Count < 100)
                    batch.Add(ev);
                if (batch.Count == 0) continue;
                try { await SendBatchAsync(batch, _cts.Token); }
                catch (Exception ex) { _log.LogDebug(ex, "Telemetry batch failed"); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log.LogDebug(ex, "Telemetry worker stopped"); }
    }

    private async Task SendBatchAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { events });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(_endpoint, content, ct);
            if (!resp.IsSuccessStatusCode)
                _log.LogDebug("Telemetry batch not accepted: {Status}", resp.StatusCode);
        }
        catch (Exception ex) { _log.LogDebug(ex, "Telemetry send failed silently"); }
    }

    private static string Redact(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[A-Za-z]:\\[^\s""']+", "<path>");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[\w\.-]+@[\w\.-]+", "<email>");
        return s.Length > 200 ? s[..200] : s;
    }

    private static string HashStack(string stack)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(stack));
        return Convert.ToHexString(bytes)[..12];
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _worker.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _cts.Dispose(); } catch { }
        try { _http.Dispose(); } catch { }
    }
}

public sealed class TelemetryEvent
{
    public string Name { get; set; } = "";
    public string AnonymousId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}
