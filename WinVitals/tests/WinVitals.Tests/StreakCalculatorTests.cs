using FluentAssertions;
using WinVitals.Core.Analytics;
using Xunit;

namespace WinVitals.Tests;

public class StreakCalculatorTests
{
    [Fact]
    public void No_Sessions_Returns_Zero()
        => StreakCalculator.Compute(Array.Empty<DateTime>(), DateTime.Now).Should().Be(0);

    [Fact]
    public void Session_Older_Than_Yesterday_Returns_Zero()
    {
        var now = new DateTime(2026, 3, 15, 10, 0, 0);
        StreakCalculator.Compute(new[] { now.AddDays(-3) }, now).Should().Be(0);
    }

    [Fact]
    public void Three_Consecutive_Days_Returns_Three()
    {
        var now = new DateTime(2026, 3, 15, 10, 0, 0);
        StreakCalculator.Compute(new[] { now, now.AddDays(-1), now.AddDays(-2) }, now).Should().Be(3);
    }

    [Fact]
    public void Gap_Breaks_Streak()
    {
        var now = new DateTime(2026, 3, 15, 10, 0, 0);
        StreakCalculator.Compute(new[] { now, now.AddDays(-1), now.AddDays(-3), now.AddDays(-4) }, now).Should().Be(2);
    }

    [Fact]
    public void Multiple_Sessions_Same_Day_Count_Once()
    {
        var now = new DateTime(2026, 3, 15, 10, 0, 0);
        StreakCalculator.Compute(new[] { now.AddHours(-1), now.AddHours(-3), now.AddDays(-1), now.AddDays(-2) }, now).Should().Be(3);
    }
}
