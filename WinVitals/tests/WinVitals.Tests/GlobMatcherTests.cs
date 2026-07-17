using FluentAssertions;
using WinVitals.Core.Rules;
using Xunit;

namespace WinVitals.Tests;

public class GlobMatcherTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\a.dll", @"**\System32\**", true)]
    [InlineData(@"C:\Users\A\node_modules\pkg\i.js", @"**\node_modules\**", true)]
    [InlineData(@"C:\a\b\c.txt", @"**\node_modules\**", false)]
    [InlineData(@"C:\src\bin\Debug\net9.0\app.dll", @"**\bin\Debug\**", true)]
    [InlineData(@"C:\Users\A\.git\config", @"**\.git\**", true)]
    [InlineData(@"C:\Users\Alice\Downloads\big.iso", @"**\Users\*\Downloads\**", true)]
    [InlineData(@"C:\Users\Alice\Bob\Downloads\big.iso", @"**\Users\*\Downloads\**", false)]
    public void Matches_Various(string path, string pattern, bool expected)
    {
        GlobMatcher.IsMatch(path, pattern).Should().Be(expected);
    }

    [Fact]
    public void IsCaseInsensitive()
    {
        GlobMatcher.IsMatch(@"C:\WINDOWS\SYSTEM32\x.dll", @"**\system32\**").Should().BeTrue();
    }
}
