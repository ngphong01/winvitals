using System.Text.Json;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Rules;

public sealed class RuleRepository
{
    private readonly string _rulesDir;
    private readonly string _customFile;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public event EventHandler? Changed;

    public RuleRepository(string rulesDir)
    {
        _rulesDir = rulesDir;
        _customFile = Path.Combine(rulesDir, "custom-rules.json");
    }

    public IReadOnlyList<CleanRule> LoadAll()
    {
        lock (_lock)
        {
            var list = new List<CleanRule>();
            list.AddRange(BuiltInRules.All);

            // Always include compiled defaults as baseline
            list.AddRange(DefaultCleanRules.All);

            // JSON rules override/add to the baseline
            if (Directory.Exists(_rulesDir))
            {
                var defaultsFile = Path.Combine(_rulesDir, "default-rules.json");
                if (File.Exists(defaultsFile))
                {
                    var jsonDefaults = TryLoad(defaultsFile);
                    foreach (var r in jsonDefaults)
                    {
                        r.IsBuiltIn = false;
                        // Replace compiled default with JSON version if same ID
                        var existing = list.FindIndex(x => x.Id == r.Id);
                        if (existing >= 0) list[existing] = r;
                        else list.Add(r);
                    }
                }

                if (File.Exists(_customFile))
                {
                    var custom = TryLoad(_customFile);
                    foreach (var r in custom) r.IsBuiltIn = false;
                    list.AddRange(custom);
                }
            }

            return list;
        }
    }

    public IReadOnlyList<CleanRule> LoadCustom()
    {
        lock (_lock)
        {
            if (!File.Exists(_customFile)) return Array.Empty<CleanRule>();
            var list = TryLoad(_customFile);
            foreach (var r in list) r.IsBuiltIn = false;
            return list;
        }
    }

    public void SaveCustom(IEnumerable<CleanRule> rules)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_rulesDir);
            var list = rules.Where(r => !r.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(list, JsonOpts);
            File.WriteAllText(_customFile, json);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpsertCustom(CleanRule rule)
    {
        if (rule.IsBuiltIn) throw new InvalidOperationException("Cannot modify built-in rule");
        if (string.IsNullOrWhiteSpace(rule.Id))
            throw new ArgumentException("Rule Id required");

        var custom = LoadCustom().ToList();
        var idx = custom.FindIndex(r => string.Equals(r.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) custom[idx] = rule;
        else custom.Add(rule);
        SaveCustom(custom);
    }

    public bool DeleteCustom(string id)
    {
        var custom = LoadCustom().ToList();
        var removed = custom.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) SaveCustom(custom);
        return removed > 0;
    }

    public void SaveDefaults()
    {
        Directory.CreateDirectory(_rulesDir);
        var target = Path.Combine(_rulesDir, "default-rules.json");
        var json = JsonSerializer.Serialize(DefaultCleanRules.All, JsonOpts);
        File.WriteAllText(target, json);
    }

    public string ExportAll()
    {
        var package = new RuleExportPackage
        {
            ExportedAtUtc = DateTime.UtcNow,
            Version = 1,
            Rules = LoadCustom().ToList()
        };
        return JsonSerializer.Serialize(package, JsonOpts);
    }

    public ImportResult ImportFromJson(string json, bool merge)
    {
        RuleExportPackage? pkg;
        try { pkg = JsonSerializer.Deserialize<RuleExportPackage>(json, JsonOpts); }
        catch (Exception ex) { return new ImportResult(false, 0, 0, ex.Message); }

        if (pkg is null || pkg.Rules is null)
            return new ImportResult(false, 0, 0, "Invalid or empty JSON");

        var incoming = pkg.Rules.Where(r => !r.IsBuiltIn).ToList();
        foreach (var r in incoming) r.IsBuiltIn = false;

        int added = 0, updated = 0;

        if (!merge)
        {
            SaveCustom(incoming);
            added = incoming.Count;
        }
        else
        {
            var existing = LoadCustom().ToList();
            var byId = existing.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var r in incoming)
            {
                if (byId.ContainsKey(r.Id))
                {
                    var idx = existing.FindIndex(x =>
                        string.Equals(x.Id, r.Id, StringComparison.OrdinalIgnoreCase));
                    existing[idx] = r;
                    updated++;
                }
                else
                {
                    existing.Add(r);
                    added++;
                }
            }
            SaveCustom(existing);
        }

        return new ImportResult(true, added, updated, null);
    }

    private static List<CleanRule> TryLoad(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<CleanRule>>(json, JsonOpts) ?? new();
        }
        catch { return new(); }
    }
}

public sealed class RuleExportPackage
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public List<CleanRule> Rules { get; set; } = new();
}

public sealed record ImportResult(bool Success, int Added, int Updated, string? Error);
