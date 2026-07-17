using FluentAssertions;
using Xunit;

namespace WinVitals.Tests;

public class BytesBucketingTests
{
    [Theory]
    [InlineData(1_000_000, "<10MB")]
    [InlineData(50_000_000, "10-100MB")]
    [InlineData(500_000_000, "100MB-1GB")]
    [InlineData(5_000_000_000L, "1-10GB")]
    [InlineData(50_000_000_000L, ">10GB")]
    public void Bucket_Correct_Range(long bytes, string expected)
    {
        BucketBytes(bytes).Should().Be(expected);
    }

    private static string BucketBytes(long b) => b switch
    {
        < 10_000_000 => "<10MB",
        < 100_000_000 => "10-100MB",
        < 1_000_000_000 => "100MB-1GB",
        < 10_000_000_000 => "1-10GB",
        _ => ">10GB"
    };
}
