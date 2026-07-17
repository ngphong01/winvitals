using FluentAssertions;
using WinVitals.Core.Rules;
using Xunit;

namespace WinVitals.Tests;

public class ExclusionFilterTests
{
    [Fact]
    public void Empty_Filter_Excludes_Nothing()
    {
        ExclusionFilter.Empty.IsExcluded(@"C:\anything\at\all").Should().BeFalse();
    }

    [Fact]
    public void Matches_Glob_Pattern()
    {
        var f = new ExclusionFilter(new[] { @"C:\Users\Anh\Sensitive\**" });
        f.IsExcluded(@"C:\Users\Anh\Sensitive\secrets.txt").Should().BeTrue();
        f.IsExcluded(@"C:\Users\Anh\Other\file.txt").Should().BeFalse();
    }

    [Fact]
    public void Is_Case_Insensitive()
    {
        var f = new ExclusionFilter(new[] { @"C:\FOO\**" });
        f.IsExcluded(@"C:\foo\bar.txt").Should().BeTrue();
    }
}
