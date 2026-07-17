using FluentAssertions;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;
using Xunit;

namespace WinVitals.Tests;

public class RuleValidatorTests
{
    private static CleanRule Good() => new()
    {
        Id = "my_rule", Name = "My", Priority = 50,
        PathPatterns = new() { @"**\x\**" },
        Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe,
        Preset = ScanPreset.Quick, Enabled = true
    };

    [Fact]
    public void Good_Rule_Has_No_Errors()
    {
        RuleValidator.Validate(Good()).Should().BeEmpty();
    }

    [Fact]
    public void Empty_Id_Fails()
    {
        var r = Good(); r.Id = "";
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("Id"));
    }

    [Fact]
    public void Uppercase_Id_Fails()
    {
        var r = Good(); r.Id = "MyRule";
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("Id"));
    }

    [Fact]
    public void Priority_Over_199_Fails()
    {
        var r = Good(); r.Priority = 250;
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("Priority"));
    }

    [Fact]
    public void No_Condition_Fails()
    {
        var r = Good(); r.PathPatterns.Clear();
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("condition"));
    }

    [Fact]
    public void Extension_Without_Dot_Fails()
    {
        var r = Good(); r.Extensions = new() { "log" };
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("dot"));
    }

    [Fact]
    public void Block_With_Safe_Risk_Fails()
    {
        var r = Good(); r.Action = ItemAction.Block; r.Risk = RiskLevel.Safe;
        RuleValidator.Validate(r).Should().Contain(e => e.Contains("Block"));
    }
}
