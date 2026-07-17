using LiteDB;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Storage;

public sealed class CleanSessionStore
{
    private readonly ILiteCollection<CleanSession> _col;

    public CleanSessionStore(LiteDbStore db)
    {
        _col = db.CleanSessions;
        _col.EnsureIndex(x => x.CompletedAtUtc);
    }

    public void Save(CleanSession session) => _col.Insert(session);

    public IReadOnlyList<CleanSession> Recent(int limit) =>
        _col.Query()
            .OrderByDescending(x => x.CompletedAtUtc)
            .Limit(limit)
            .ToList();

    public IReadOnlyList<CleanSession> Between(DateTime fromUtc, DateTime toUtc) =>
        _col.Query()
            .Where(x => x.CompletedAtUtc >= fromUtc && x.CompletedAtUtc <= toUtc)
            .OrderBy(x => x.CompletedAtUtc)
            .ToList();

    public long TotalBytesEver() =>
        _col.Query().ToList().Sum(x => x.BytesFreed);

    public int TotalSessions() => _col.Count();

    public CleanSession? MostRecent() =>
        _col.Query().OrderByDescending(x => x.CompletedAtUtc).FirstOrDefault();
}
