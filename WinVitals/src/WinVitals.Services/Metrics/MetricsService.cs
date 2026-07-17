using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinVitals.Core.Entities;
using WinVitals.Core.Probes;

namespace WinVitals.Services.Metrics;

public sealed class MetricsService : BackgroundService, IMetricsService
{
    private readonly Channel<PerfSample> _channel =
        Channel.CreateBounded<PerfSample>(new BoundedChannelOptions(120)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        });

    private readonly ILogger<MetricsService> _log;
    public ChannelReader<PerfSample> Stream => _channel.Reader;
    public PerfSample? Latest { get; private set; }

    public MetricsService(ILogger<MetricsService> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var cpu = new CpuProbe();
        using var disk = new DiskProbe();
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var cpuPct = cpu.Read();
                var (ramUsed, ramTotal) = MemoryProbe.Read();
                var (dr, dw) = disk.Read();
                var health = ComputeHealthScore(cpuPct, ramUsed / Math.Max(ramTotal, 1) * 100);
                var sample = new PerfSample(DateTime.UtcNow, cpuPct, ramUsed, ramTotal, dr, dw, health);
                Latest = sample;
                await _channel.Writer.WriteAsync(sample, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Metrics tick failed");
            }
        }
    }

    private static int ComputeHealthScore(double cpu, double ramPct)
    {
        var score = 100.0;
        if (cpu > 80) score -= (cpu - 80) * 1.5;
        if (ramPct > 80) score -= (ramPct - 80) * 1.2;
        return Math.Clamp((int)Math.Round(score), 0, 100);
    }
}
