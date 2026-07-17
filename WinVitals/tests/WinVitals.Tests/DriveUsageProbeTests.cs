using FluentAssertions;
using WinVitals.Core.Probes;
using Xunit;

namespace WinVitals.Tests;

public class DriveUsageProbeTests
{
    [Fact]
    public void Reads_At_Least_C_Drive()
    {
        var drives = DriveUsageProbe.Read();
        drives.Should().NotBeEmpty();
        drives.Should().Contain(d => d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Percentages_Are_In_Range()
    {
        foreach (var d in DriveUsageProbe.Read())
        {
            d.UsedPercent.Should().BeInRange(0, 100);
            d.TotalBytes.Should().BeGreaterThan(0);
            d.FreeBytes.Should().BeLessThanOrEqualTo(d.TotalBytes);
        }
    }
}
