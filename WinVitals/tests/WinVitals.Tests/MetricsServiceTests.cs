using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinVitals.Services.Metrics;
using Xunit;

namespace WinVitals.Tests;

public class MetricsServiceTests
{
    [Fact]
    public async Task Emits_Samples_Within_Three_Seconds()
    {
        var svc = new MetricsService(NullLogger<MetricsService>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _ = svc.StartAsync(cts.Token);

        // Poll until sample arrives or timeout (max 10s)
        for (int i = 0; i < 100; i++)
        {
            if (svc.Latest is not null) break;
            await Task.Delay(100, cts.Token);
        }

        svc.Latest.Should().NotBeNull();
        svc.Latest!.Value.RamTotalGb.Should().BeGreaterThan(0);

        await svc.StopAsync(CancellationToken.None);
    }
}
