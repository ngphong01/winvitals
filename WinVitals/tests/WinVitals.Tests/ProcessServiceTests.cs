using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinVitals.Services.Processes;
using Xunit;

namespace WinVitals.Tests;

public class ProcessServiceTests
{
    [Fact]
    public async Task RefreshAsync_Runs_On_Background_And_Returns_Data()
    {
        var svc = new ProcessService(NullLogger<ProcessService>.Instance);
        var snap = await svc.RefreshAsync();
        snap.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Two_Consecutive_Refreshes_Yield_Cpu_Values()
    {
        var svc = new ProcessService(NullLogger<ProcessService>.Instance);
        await svc.RefreshAsync();
        await Task.Delay(600);
        var second = await svc.RefreshAsync();
        second.Any(p => p.CpuPercent > 0).Should().BeTrue();
    }

    [Fact]
    public async Task EndProcess_On_Invalid_Pid_Returns_False()
    {
        var svc = new ProcessService(NullLogger<ProcessService>.Instance);
        (await svc.EndProcessAsync(-1)).Should().BeFalse();
    }
}
