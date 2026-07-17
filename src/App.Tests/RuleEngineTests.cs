using App.Core;
using App.Cleaner;
using FluentAssertions;
using Xunit;

namespace App.Tests;

public class RuleEngineTests
{
    private static string CreateRulesDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"whm_rules_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test-rules.json"), """
        [
            {"id":"temp_safe","name":"Temp safe","pathPatterns":["**\\Temp\\**"],"extensions":[],"minSizeBytes":0,"maxAgeDays":0,"action":"SafeDelete","risk":"Safe","priority":80,"enabled":true,"cleanLevel":"Quick"},
            {"id":"dmp_ext","name":"DMP by ext","pathPatterns":[],"extensions":[".dmp",".mdmp"],"minSizeBytes":0,"maxAgeDays":0,"action":"SafeDelete","risk":"Safe","priority":75,"enabled":true,"cleanLevel":"Quick"},
            {"id":"large_old","name":"Large old","pathPatterns":[],"extensions":[".iso",".zip"],"minSizeBytes":524288000,"maxAgeDays":180,"action":"WarnDelete","risk":"Medium","priority":50,"enabled":true,"cleanLevel":"Deep"},
            {"id":"dev_cache","name":"Dev cache","pathPatterns":["**\\node_modules\\**","**\\__pycache__\\**"],"extensions":[],"minSizeBytes":0,"maxAgeDays":0,"action":"SafeDelete","risk":"Low","priority":70,"enabled":true,"cleanLevel":"Developer"}
        ]
        """);
        return dir;
    }

    [Fact] public async Task LoadRules_HasMinCount() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.GetRules().Count.Should().BeGreaterThanOrEqualTo(9); }
    [Fact] public async Task TempFile_SafeDelete() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\Users\me\AppData\Local\Temp\f.tmp").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task DmpFile_SafeDelete() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\Minidump\c.dmp").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task SmallNewIso_DefaultsWarn() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\d\s.iso", 1_048_576, DateTime.Now).Item1.Should().Be(ItemAction.WarnDelete); }
    [Fact] public async Task LargeOldIso_WarnDelete() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); var (a, r, n) = e.Evaluate(@"C:\d\b.iso", 629_145_600, DateTime.Now.AddDays(-200)); a.Should().Be(ItemAction.WarnDelete); n.Should().Be("Large old"); }
    [Fact] public async Task NodeModules_SafeDelete() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\p\node_modules\l\i.js").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task System32_Blocked() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); var (a, r, _) = e.Evaluate(@"C:\Windows\System32\n.dll"); a.Should().Be(ItemAction.Block); r.Should().Be(RiskLevel.Critical); }
    [Fact] public async Task Priority_HigherWins() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\Users\me\AppData\Local\Temp\x.dmp").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task Toggle_DisableReEnable() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); await e.ToggleRule("temp_safe"); e.Evaluate(@"C:\Users\me\AppData\Local\Temp\f.tmp").Item1.Should().NotBe(ItemAction.SafeDelete); await e.ToggleRule("temp_safe"); e.Evaluate(@"C:\Users\me\AppData\Local\Temp\f.tmp").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task Extensions_CaseInsensitive() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.Evaluate(@"C:\t\d.DMP").Item1.Should().Be(ItemAction.SafeDelete); e.Evaluate(@"C:\t\d.dmp").Item1.Should().Be(ItemAction.SafeDelete); }
    [Fact] public async Task AddRemove_CustomRule() { using var t = new TempDir(CreateRulesDir()); var e = new RuleEngine(t.Path); await e.LoadRulesAsync(); e.AddRule(new Rule { Id = "x", Name = "X", Priority = 90, Extensions = [".xyz"], Action = ItemAction.Block, Enabled = true }); e.Evaluate(@"C:\f.xyz").Item1.Should().Be(ItemAction.Block); e.RemoveRule("x"); e.Evaluate(@"C:\f.xyz").Item1.Should().NotBe(ItemAction.Block); }
}
