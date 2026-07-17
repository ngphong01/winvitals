using FluentAssertions;
using WinVitals.Core.Probes;
using Xunit;

namespace WinVitals.Tests;

public class ProcessProbeTests
{
    [Fact]
    public void Snapshot_Returns_Current_Process()
    {
        var probe = new ProcessProbe();
        var snap = probe.Snapshot();
        var self = System.Diagnostics.Process.GetCurrentProcess().Id;

        snap.Should().NotBeEmpty();
        snap.Should().Contain(p => p.Pid == self);
    }

    [Fact]
    public void Snapshot_Fields_Are_Sane()
    {
        var probe = new ProcessProbe();
        var snap = probe.Snapshot();
        var self = snap.First(p => p.Pid == System.Diagnostics.Process.GetCurrentProcess().Id);

        self.Name.Should().NotBeNullOrWhiteSpace();
        self.WorkingSetBytes.Should().BeGreaterThan(0);
        self.ThreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Cpu_Delta_Computed_On_Second_Call()
    {
        var probe = new ProcessProbe();
        _ = probe.Snapshot();
        Thread.Sleep(500);
        var s2 = probe.Snapshot();

        s2.Any(p => p.CpuPercent > 0).Should().BeTrue("at least one process should show CPU");
    }
}
