using FluentAssertions;
using WinVitals.Core.Entities;
using WinVitals.Services.Scheduling;
using Xunit;

namespace WinVitals.Tests;

public class SchedulerNextRunTests
{
    private static AppSettings Base(ScheduleFrequency f, int hour = 3, int minute = 0)
        => new() { ScheduleFrequency = f, ScheduleTime = new TimeOnly(hour, minute) };

    [Fact]
    public void Never_Returns_Null()
    {
        var next = SchedulerService.ComputeNextRun(Base(ScheduleFrequency.Never), DateTime.UtcNow);
        next.Should().BeNull();
    }

    [Fact]
    public void Daily_Before_Time_Runs_Today()
    {
        var now = new DateTime(2026, 1, 15, 2, 30, 0, DateTimeKind.Local);
        var next = SchedulerService.ComputeNextRun(Base(ScheduleFrequency.Daily), now.ToUniversalTime());
        next!.Value.ToLocalTime().Day.Should().Be(15);
        next.Value.ToLocalTime().Hour.Should().Be(3);
    }

    [Fact]
    public void Daily_After_Time_Runs_Tomorrow()
    {
        var now = new DateTime(2026, 1, 15, 4, 0, 0, DateTimeKind.Local);
        var next = SchedulerService.ComputeNextRun(Base(ScheduleFrequency.Daily), now.ToUniversalTime());
        next!.Value.ToLocalTime().Day.Should().Be(16);
    }

    [Fact]
    public void Weekly_Picks_Correct_Day()
    {
        var s = Base(ScheduleFrequency.Weekly);
        s.ScheduleDayOfWeek = DayOfWeek.Sunday;
        var now = new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Local);
        var next = SchedulerService.ComputeNextRun(s, now.ToUniversalTime());
        next!.Value.ToLocalTime().DayOfWeek.Should().Be(DayOfWeek.Sunday);
        next.Value.ToLocalTime().Day.Should().Be(18);
    }

    [Fact]
    public void Monthly_Skips_To_Next_Month_If_Past()
    {
        var s = Base(ScheduleFrequency.Monthly);
        s.ScheduleDayOfMonth = 5;
        var now = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Local);
        var next = SchedulerService.ComputeNextRun(s, now.ToUniversalTime());
        next!.Value.ToLocalTime().Month.Should().Be(2);
        next.Value.ToLocalTime().Day.Should().Be(5);
    }

    [Fact]
    public void Monthly_Clamps_Day_To_28()
    {
        var s = Base(ScheduleFrequency.Monthly);
        s.ScheduleDayOfMonth = 31;
        var now = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Local);
        var next = SchedulerService.ComputeNextRun(s, now.ToUniversalTime());
        next!.Value.ToLocalTime().Day.Should().Be(28);
    }
}
