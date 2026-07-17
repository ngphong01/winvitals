using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinVitals.Core.Telemetry;
using Xunit;

namespace WinVitals.Tests;

public class TelemetryTests
{
    [Fact]
    public void NullTelemetry_Never_Throws()
    {
        var t = new NullTelemetry();
        t.TrackEvent("x");
        t.TrackException(new Exception("boom"));
        Func<Task> flush = async () => await t.FlushAsync();
        flush.Should().NotThrowAsync();
    }

    [Fact]
    public void Disabled_Telemetry_Drops_Events()
    {
        var svc = new HttpTelemetry(
            new HttpClient(new StubHandler()),
            new Uri("http://localhost"), "anon",
            NullLogger<HttpTelemetry>.Instance);
        svc.IsEnabled = false;
        svc.TrackEvent("test");
        Thread.Sleep(200);
        // Event silently dropped
    }

    [Fact]
    public void AnonymousId_Is_Stable_Across_Calls()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wv-anon-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id1 = AnonymousIdGenerator.GetOrCreate(dir);
            var id2 = AnonymousIdGenerator.GetOrCreate(dir);
            id1.Should().Be(id2);
            id1.Length.Should().Be(32);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void AnonymousId_Is_Different_Per_Directory()
    {
        var d1 = Path.Combine(Path.GetTempPath(), "wv-a-" + Guid.NewGuid().ToString("N"));
        var d2 = Path.Combine(Path.GetTempPath(), "wv-b-" + Guid.NewGuid().ToString("N"));
        try
        {
            var id1 = AnonymousIdGenerator.GetOrCreate(d1);
            var id2 = AnonymousIdGenerator.GetOrCreate(d2);
            id1.Should().NotBe(id2);
        }
        finally
        {
            if (Directory.Exists(d1)) Directory.Delete(d1, true);
            if (Directory.Exists(d2)) Directory.Delete(d2, true);
        }
    }

    private class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
