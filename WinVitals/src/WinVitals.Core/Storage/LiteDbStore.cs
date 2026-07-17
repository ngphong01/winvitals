using LiteDB;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Storage;

public sealed class LiteDbStore : IDisposable
{
    public LiteDatabase Db { get; }

    public LiteDbStore(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        Db = new LiteDatabase($"Filename={path};Connection=shared");
    }

    public ILiteCollection<QuarantineEntry> Quarantine => Db.GetCollection<QuarantineEntry>("quarantine");
    public ILiteCollection<CleanRule> Rules => Db.GetCollection<CleanRule>("rules");
    public ILiteCollection<CleanSession> CleanSessions => Db.GetCollection<CleanSession>("clean_sessions");

    public void Dispose() => Db.Dispose();
}
