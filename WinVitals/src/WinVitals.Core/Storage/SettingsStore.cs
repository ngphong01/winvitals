using LiteDB;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Storage;

public sealed class SettingsStore
{
    private readonly ILiteCollection<AppSettings> _col;
    private AppSettings? _cache;
    private readonly object _lock = new();

    public SettingsStore(LiteDbStore db) => _col = db.Db.GetCollection<AppSettings>("settings");

    public AppSettings Get()
    {
        lock (_lock)
        {
            if (_cache is not null) return _cache;
            _cache = _col.FindById(1) ?? new AppSettings();
            if (_col.FindById(1) is null) _col.Insert(_cache);
            return _cache;
        }
    }

    public void Save(AppSettings s)
    {
        lock (_lock)
        {
            s.Id = 1;
            _col.Upsert(s);
            _cache = s;
        }
    }

    public bool IsOnboardingCompleted() => Get().OnboardingCompleted;

    public void SetOnboardingCompleted()
    {
        var s = Get();
        s.OnboardingCompleted = true;
        Save(s);
    }
}
