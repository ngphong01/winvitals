using System.Diagnostics;

namespace WinVitals.Core.Probes;

public sealed class DiskProbe : IDisposable
{
    private readonly PerformanceCounter _read = new("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
    private readonly PerformanceCounter _write = new("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

    public (double readMBps, double writeMBps) Read()
    {
        var r = _read.NextValue() / 1024.0 / 1024.0;
        var w = _write.NextValue() / 1024.0 / 1024.0;
        return (Math.Round(r, 2), Math.Round(w, 2));
    }

    public void Dispose() { _read.Dispose(); _write.Dispose(); }
}
