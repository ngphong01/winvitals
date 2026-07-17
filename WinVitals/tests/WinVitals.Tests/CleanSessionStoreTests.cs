using FluentAssertions;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;
using Xunit;

namespace WinVitals.Tests;

public sealed class CleanSessionStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly LiteDbStore _db;
    private readonly CleanSessionStore _store;

    public CleanSessionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wv-sess-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _db = new LiteDbStore(Path.Combine(_dir, "t.db"));
        _store = new CleanSessionStore(_db);
    }
    public void Dispose() { _db.Dispose(); try { Directory.Delete(_dir, true); } catch { } }

    private static CleanSession Make(DateTime c, long bytes, ScanPreset p = ScanPreset.Quick, bool sched = false) => new()
    {
        StartedAtUtc = c.AddSeconds(-30),
        CompletedAtUtc = c,
        BytesFreed = bytes,
        QuarantinedCount = 10,
        Preset = p,
        WasScheduled = sched
    };

    [Fact] public void Save_And_Retrieve_Recent() { _store.Save(Make(DateTime.UtcNow, 1000)); _store.Save(Make(DateTime.UtcNow.AddMinutes(-5), 500)); _store.Recent(10).Should().HaveCount(2); }
    [Fact] public void Recent_Orders_Desc() { _store.Save(Make(DateTime.UtcNow.AddHours(-1), 100)); _store.Save(Make(DateTime.UtcNow, 200)); _store.Recent(10)[0].BytesFreed.Should().Be(200); }
    [Fact] public void Between_Filters() { _store.Save(Make(DateTime.UtcNow.AddDays(-10), 100)); _store.Save(Make(DateTime.UtcNow.AddDays(-1), 200)); _store.Between(DateTime.UtcNow.AddDays(-15), DateTime.UtcNow).Should().HaveCount(2); }
    [Fact] public void TotalBytesEver_Sums() { _store.Save(Make(DateTime.UtcNow, 1000)); _store.Save(Make(DateTime.UtcNow.AddHours(-1), 2000)); _store.TotalBytesEver().Should().Be(3000); }
    [Fact] public void MostRecent_Returns_Latest() { _store.Save(Make(DateTime.UtcNow.AddDays(-2), 100)); _store.Save(Make(DateTime.UtcNow, 999)); _store.MostRecent()!.BytesFreed.Should().Be(999); }
    [Fact] public void Persists_Categories() { var s = Make(DateTime.UtcNow, 500); s.BytesByCategory[ItemCategory.TempFile] = 300; s.CountByCategory[ItemCategory.TempFile] = 5; _store.Save(s); var b = _store.Recent(1).Single(); b.BytesByCategory[ItemCategory.TempFile].Should().Be(300); b.CountByCategory[ItemCategory.TempFile].Should().Be(5); }
}
