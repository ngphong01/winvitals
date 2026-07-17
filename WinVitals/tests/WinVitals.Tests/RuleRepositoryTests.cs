using FluentAssertions;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;
using Xunit;

namespace WinVitals.Tests;

public sealed class RuleRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly RuleRepository _repo;

    public RuleRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wv-rules-" + Guid.NewGuid().ToString("N"));
        _repo = new RuleRepository(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static CleanRule Sample(string id = "my_rule") => new()
    {
        Id = id, Name = "My Rule", Priority = 50,
        PathPatterns = new() { @"**\my\**" },
        Action = ItemAction.SafeDelete, Risk = RiskLevel.Low,
        Preset = ScanPreset.Quick, Enabled = true
    };

    [Fact]
    public void LoadAll_Includes_BuiltIn_And_Defaults()
    {
        var all = _repo.LoadAll();
        all.Any(r => r.IsBuiltIn).Should().BeTrue();
        all.Any(r => !r.IsBuiltIn).Should().BeTrue();
    }

    [Fact]
    public void UpsertCustom_Adds_And_Persists()
    {
        _repo.UpsertCustom(Sample());
        var all = _repo.LoadAll();
        all.Should().ContainSingle(r => r.Id == "my_rule" && !r.IsBuiltIn);
    }

    [Fact]
    public void UpsertCustom_Updates_Existing()
    {
        _repo.UpsertCustom(Sample());
        var updated = Sample();
        updated.Name = "Renamed";
        _repo.UpsertCustom(updated);
        _repo.LoadCustom().Single().Name.Should().Be("Renamed");
    }

    [Fact]
    public void UpsertCustom_Rejects_BuiltIn()
    {
        var builtIn = Sample();
        builtIn.IsBuiltIn = true;
        var act = () => _repo.UpsertCustom(builtIn);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeleteCustom_Removes_Rule()
    {
        _repo.UpsertCustom(Sample());
        _repo.DeleteCustom("my_rule").Should().BeTrue();
        _repo.LoadCustom().Should().BeEmpty();
    }

    [Fact]
    public void DeleteCustom_Returns_False_For_Unknown()
    {
        _repo.DeleteCustom("does_not_exist").Should().BeFalse();
    }

    [Fact]
    public void Changed_Event_Fires_On_Save()
    {
        int count = 0;
        _repo.Changed += (_, _) => count++;
        _repo.UpsertCustom(Sample());
        _repo.UpsertCustom(Sample("another"));
        count.Should().Be(2);
    }

    [Fact]
    public void Export_Import_Roundtrip()
    {
        _repo.UpsertCustom(Sample("r1"));
        _repo.UpsertCustom(Sample("r2"));
        var json = _repo.ExportAll();
        _repo.DeleteCustom("r1");
        _repo.DeleteCustom("r2");
        _repo.LoadCustom().Should().BeEmpty();
        var res = _repo.ImportFromJson(json, merge: false);
        res.Success.Should().BeTrue();
        res.Added.Should().Be(2);
        _repo.LoadCustom().Should().HaveCount(2);
    }

    [Fact]
    public void Import_Merge_Updates_And_Adds()
    {
        _repo.UpsertCustom(Sample("existing"));
        var pkg = new RuleExportPackage { Rules = new() {
            new CleanRule { Id = "existing", Name = "Updated", Priority = 10, PathPatterns = new() { @"**\x\**" },
                Action = ItemAction.WarnDelete, Risk = RiskLevel.Medium, Enabled = true, Preset = ScanPreset.Quick },
            new CleanRule { Id = "newone", Name = "New", Priority = 10, PathPatterns = new() { @"**\y\**" },
                Action = ItemAction.SafeDelete, Risk = RiskLevel.Safe, Enabled = true, Preset = ScanPreset.Quick }
        }};
        var json = System.Text.Json.JsonSerializer.Serialize(pkg);
        var res = _repo.ImportFromJson(json, merge: true);
        res.Added.Should().Be(1);
        res.Updated.Should().Be(1);
        _repo.LoadCustom().Should().HaveCount(2);
    }

    [Fact]
    public void Import_Invalid_Json_Returns_Error()
    {
        var res = _repo.ImportFromJson("not json at all", merge: false);
        res.Success.Should().BeFalse();
        res.Error.Should().NotBeNullOrEmpty();
    }
}
