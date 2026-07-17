using FluentAssertions;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;
using Xunit;

namespace WinVitals.Tests;

public class RuleEvaluatorTests
{
    private static RuleEvaluator BuildDefault()
    {
        var rules = new List<CleanRule>();
        rules.AddRange(BuiltInRules.All);
        rules.AddRange(DefaultCleanRules.All);
        return new RuleEvaluator(rules);
    }

    [Fact]
    public void System32_Is_Blocked_Critical()
    {
        var r = BuildDefault().Evaluate(@"C:\Windows\System32\kernel32.dll", 1000, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
        r.Risk.Should().Be(RiskLevel.Critical);
    }

    [Fact]
    public void SysWOW64_Is_Blocked()
    {
        var r = BuildDefault().Evaluate(@"C:\Windows\SysWOW64\anything.dll", 100, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
    }

    [Fact]
    public void WinSxS_Is_Blocked()
    {
        var r = BuildDefault().Evaluate(@"C:\Windows\WinSxS\file.dll", 100, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
    }

    [Fact]
    public void Drivers_Folder_Is_Blocked()
    {
        var r = BuildDefault().Evaluate(@"C:\Windows\System32\drivers\etc\hosts", 100, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
    }

    [Fact]
    public void EnvFile_Is_Blocked_Critical()
    {
        var r = BuildDefault().Evaluate(@"C:\Users\Anh\project\.env", 200, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
        r.Risk.Should().Be(RiskLevel.Critical);
    }

    [Fact]
    public void GitFolder_Is_Blocked()
    {
        var r = BuildDefault().Evaluate(@"C:\Users\Anh\repo\.git\config", 100, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
    }

    [Fact]
    public void DbFile_Is_Blocked()
    {
        var r = BuildDefault().Evaluate(@"C:\Users\Anh\app\data.sqlite", 500_000, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
    }

    [Fact]
    public void TempFile_Is_SafeDelete()
    {
        var r = BuildDefault().Evaluate(@"C:\Users\Anh\AppData\Local\Temp\x.tmp", 100, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.SafeDelete);
        r.Risk.Should().Be(RiskLevel.Safe);
        r.RuleId.Should().Be("temp_files");
    }

    [Fact]
    public void CrashDump_Is_SafeDelete()
    {
        var r = BuildDefault().Evaluate(@"C:\dumps\crash.dmp", 5_000_000, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.SafeDelete);
        r.RuleId.Should().Be("crash_dumps");
    }

    [Fact]
    public void NodeModules_Is_SafeDelete()
    {
        var r = BuildDefault().Evaluate(
            @"C:\Users\Anh\projects\web\node_modules\react\index.js", 500, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.SafeDelete);
        r.RuleId.Should().Be("dev_node_modules");
    }

    [Fact]
    public void PythonCache_Is_SafeDelete()
    {
        var r = BuildDefault().Evaluate(
            @"C:\Users\Anh\ml\src\__pycache__\main.cpython-312.pyc", 800, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.SafeDelete);
        r.RuleId.Should().Be("dev_python_cache");
    }

    [Fact]
    public void SmallNewIso_Does_Not_Match_OldInstaller()
    {
        // < 100MB, mới tinh → không match
        var r = BuildDefault().Evaluate(@"C:\Downloads\tool.iso", 10_000_000, DateTime.UtcNow);
        r.RuleId.Should().NotBe("old_installers");
    }

    [Fact]
    public void LargeOldIso_Matches_OldInstaller()
    {
        var r = BuildDefault().Evaluate(
            @"C:\Downloads\ubuntu.iso",
            2L * 1024 * 1024 * 1024,          // 2 GB
            DateTime.UtcNow.AddDays(-120));   // 120 ngày trước
        r.RuleId.Should().Be("old_installers");
        r.Action.Should().Be(ItemAction.WarnDelete);
    }

    [Fact]
    public void OldLog_Matches_When_Older_Than_30_Days()
    {
        var r = BuildDefault().Evaluate(
            @"C:\logs\app.log", 5000, DateTime.UtcNow.AddDays(-45));
        r.RuleId.Should().Be("old_logs");
    }

    [Fact]
    public void UnknownFile_Falls_Back_To_Default_Warn_Medium()
    {
        var r = BuildDefault().Evaluate(@"C:\SomeApp\readme.md", 200, DateTime.UtcNow);
        r.RuleId.Should().Be("default");
        r.Action.Should().Be(ItemAction.WarnDelete);
        r.Risk.Should().Be(RiskLevel.Medium);
    }

    [Fact]
    public void HigherPriority_Wins_Over_Lower()
    {
        // File .db trong Temp: temp_files (P=80) vs protect_db (P=190) → phải block
        var r = BuildDefault().Evaluate(
            @"C:\Users\Anh\AppData\Local\Temp\cache.db", 5000, DateTime.UtcNow);
        r.Action.Should().Be(ItemAction.Block);
        r.RuleId.Should().Be("protect_db");
    }
}
