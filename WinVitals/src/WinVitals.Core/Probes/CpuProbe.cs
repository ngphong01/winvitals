using System.Diagnostics;

namespace WinVitals.Core.Probes;

public sealed class CpuProbe : IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private bool _warmedUp;

    public double Read()
    {
        if (!_warmedUp)
        {
            _ = _cpuCounter.NextValue();
            Thread.Sleep(50);
            _warmedUp = true;
        }
        return Math.Round(_cpuCounter.NextValue(), 1);
    }

    public void Dispose() => _cpuCounter.Dispose();
}
