using System.Threading.Channels;
using WinVitals.Core.Entities;

namespace WinVitals.Services.Metrics;

public interface IMetricsService
{
    ChannelReader<PerfSample> Stream { get; }
    PerfSample? Latest { get; }
}
