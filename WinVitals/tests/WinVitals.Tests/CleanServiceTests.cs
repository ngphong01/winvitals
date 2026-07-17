using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinVitals.Core.Entities;
using WinVitals.Core;
using WinVitals.Core.Rules;
using WinVitals.Core.Storage;
using WinVitals.Services.Cleaning;
using WinVitals.Services.Quarantine;
using Xunit;

namespace WinVitals.Tests;

public sealed class CleanServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly LiteDbStore _store;
    private readonly QuarantineService _q;
    private readonly CleanService _clean;

    public CleanServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "wv-clean-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _store = new LiteDbStore(Path.Combine(_workDir, "t.db"));
        _q = new QuarantineService(_store, NullLogger<QuarantineService>.Instance,
            Path.Combine(_workDir, "q"));

        var rulesDir = Path.Combine(_workDir, "rules");
        var repo = new RuleRepository(rulesDir);
        _clean = new CleanService(_q, repo, new WinVitals.Core.Storage.CleanSessionStore(_store), new WinVitals.Core.Storage.SettingsStore(_store), NullLogger<CleanService>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_workDir, recursive: true); } catch { }
    }

    private ScanItem MakeItem(string relPath, ItemAction action, RiskLevel risk,
        ItemCategory cat = ItemCategory.TempFile)
    {
        var full = Path.Combine(_workDir, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, new byte[100]);
        return new ScanItem(full, 100, DateTime.UtcNow, cat, risk, action, "test");
    }

    [Fact]
    public async Task Preview_Does_Not_Touch_Files()
    {
        var item = MakeItem("a.tmp", ItemAction.SafeDelete, RiskLevel.Safe);
        var report = await _clean.PreviewAsync(new[] { item }, null, default);

        report.Quarantined.Should().Be(1);
        File.Exists(item.Path).Should().BeTrue("preview must not delete");
    }

    [Fact]
    public async Task Execute_Moves_Safe_Files_To_Quarantine()
    {
        var item = MakeItem("b.tmp", ItemAction.SafeDelete, RiskLevel.Safe);
        var report = await _clean.ExecuteAsync(new[] { item }, null, default);

        report.Quarantined.Should().Be(1);
        report.BytesFreed.Should().Be(100);
        File.Exists(item.Path).Should().BeFalse();
        _q.GetActive().Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_Blocks_Protected_Paths_At_Runtime()
    {
        var sysDir = Path.Combine(_workDir, "Windows", "System32");
        Directory.CreateDirectory(sysDir);
        var f = Path.Combine(sysDir, "fake.dll");
        File.WriteAllBytes(f, new byte[50]);

        var item = new ScanItem(f, 50, DateTime.UtcNow,
            ItemCategory.Unknown, RiskLevel.Safe, ItemAction.SafeDelete, "stale");

        var report = await _clean.ExecuteAsync(new[] { item }, null, default);

        report.Quarantined.Should().Be(0);
        report.Skipped.Should().Be(1);
        File.Exists(f).Should().BeTrue("blocked file must remain");
    }

    [Fact]
    public async Task Execute_Skips_Missing_Files_Without_Error()
    {
        var item = new ScanItem(
            Path.Combine(_workDir, "ghost.tmp"), 0, DateTime.UtcNow,
            ItemCategory.TempFile, RiskLevel.Safe, ItemAction.SafeDelete, "temp_files");

        var report = await _clean.ExecuteAsync(new[] { item }, null, default);
        report.Skipped.Should().Be(1);
        report.Failed.Should().Be(0);
    }

    [Fact]
    public async Task Execute_Is_Idempotent_Second_Run_Skips_All()
    {
        var item = MakeItem("i.tmp", ItemAction.SafeDelete, RiskLevel.Safe);

        var first = await _clean.ExecuteAsync(new[] { item }, null, default);
        var second = await _clean.ExecuteAsync(new[] { item }, null, default);

        first.Quarantined.Should().Be(1);
        second.Quarantined.Should().Be(0);
        second.Skipped.Should().Be(1);
    }
}
