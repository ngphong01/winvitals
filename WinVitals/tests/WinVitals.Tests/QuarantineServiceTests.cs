using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinVitals.Core;
using WinVitals.Core.Storage;
using WinVitals.Services.Quarantine;
using Xunit;

namespace WinVitals.Tests;

public sealed class QuarantineServiceTests : IDisposable
{
    private readonly string _workDir;
    private readonly LiteDbStore _store;
    private readonly QuarantineService _svc;

    public QuarantineServiceTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "wv-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _store = new LiteDbStore(Path.Combine(_workDir, "test.db"));
        _svc = new QuarantineService(_store, NullLogger<QuarantineService>.Instance,
            Path.Combine(_workDir, "quarantine"));
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_workDir, recursive: true); } catch { }
    }

    private string CreateFile(string name, int size = 128)
    {
        var path = Path.Combine(_workDir, name);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    [Fact]
    public async Task Quarantine_Moves_File_And_Creates_Entry()
    {
        var f = CreateFile("a.tmp", 256);
        var entry = await _svc.QuarantineAsync(f, "test", RiskLevel.Safe);

        entry.Should().NotBeNull();
        File.Exists(f).Should().BeFalse("original must be moved");
        File.Exists(entry!.QuarantinePath).Should().BeTrue();
        entry.SizeBytes.Should().Be(256);
        entry.Status.Should().Be(QuarantineStatus.Active);
        _svc.GetActive().Should().ContainSingle();
    }

    [Fact]
    public async Task Quarantine_Missing_File_Returns_Null()
    {
        var entry = await _svc.QuarantineAsync(Path.Combine(_workDir, "nope.tmp"),
            "test", RiskLevel.Safe);
        entry.Should().BeNull();
    }

    [Fact]
    public async Task Restore_Moves_File_Back()
    {
        var f = CreateFile("b.tmp");
        var e = await _svc.QuarantineAsync(f, "test", RiskLevel.Safe);
        var ok = await _svc.RestoreAsync(e!.Id);

        ok.Should().BeTrue();
        File.Exists(f).Should().BeTrue();
        _svc.GetActive().Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_When_Target_Exists_Adds_Suffix()
    {
        var f = CreateFile("c.tmp");
        var e = await _svc.QuarantineAsync(f, "test", RiskLevel.Safe);

        File.WriteAllText(f, "new content");
        var ok = await _svc.RestoreAsync(e!.Id);

        ok.Should().BeTrue();
        var files = Directory.GetFiles(_workDir, "c*.tmp");
        files.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Purge_Deletes_Quarantine_File()
    {
        var f = CreateFile("d.tmp");
        var e = await _svc.QuarantineAsync(f, "test", RiskLevel.Safe);
        var ok = await _svc.PurgeAsync(e!.Id);

        ok.Should().BeTrue();
        File.Exists(e.QuarantinePath).Should().BeFalse();
        _svc.GetActive().Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeExpired_Only_Removes_Past_Expiry()
    {
        var f1 = CreateFile("old.tmp");
        var f2 = CreateFile("new.tmp");

        var e1 = await _svc.QuarantineAsync(f1, "old", RiskLevel.Safe,
            retention: TimeSpan.FromMilliseconds(-1000));
        var e2 = await _svc.QuarantineAsync(f2, "new", RiskLevel.Safe,
            retention: TimeSpan.FromDays(7));

        var count = await _svc.PurgeExpiredAsync();
        count.Should().Be(1);
        _svc.GetActive().Should().ContainSingle(x => x.Id == e2!.Id);
    }

    [Fact]
    public async Task TotalQuarantinedBytes_Sums_Active_Only()
    {
        await _svc.QuarantineAsync(CreateFile("a", 100), "r", RiskLevel.Safe);
        await _svc.QuarantineAsync(CreateFile("b", 200), "r", RiskLevel.Safe);
        var e = await _svc.QuarantineAsync(CreateFile("c", 400), "r", RiskLevel.Safe);
        await _svc.PurgeAsync(e!.Id);

        _svc.TotalQuarantinedBytes.Should().Be(300);
    }
}
