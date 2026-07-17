using System.Text.Json;
using App.Core;

namespace App.Cleaner;

/// <summary>
/// Rule Engine - loads rules from JSON and evaluates files
/// </summary>
public class RuleEngine : IRuleEngine
{
    private readonly List<Rule> _rules = [];
    private readonly string _rulesDir;

    public RuleEngine(string rulesDir)
    {
        _rulesDir = rulesDir;
    }

    public async Task LoadRulesAsync()
    {
        _rules.Clear();
        var jsonFiles = Directory.GetFiles(_rulesDir, "*.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        foreach (var file in jsonFiles)
        {
            if (file.Contains("protected")) continue;
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var rules = JsonSerializer.Deserialize<List<Rule>>(json, options);
                if (rules != null) _rules.AddRange(rules);
                else System.Diagnostics.Debug.WriteLine("[RuleEngine] Deserialized null from " + file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RuleEngine] Failed to load {file}: {ex.Message}");
            }
        }

        // Add system-critical built-in rules
        _rules.Add(new Rule
        {
            Id = "sys_windows",
            Name = "Windows System",
            Description = "Protect Windows system files",
            PathPatterns = ["**\\Windows\\System32\\**", "**\\Windows\\SysWOW64\\**",
                          "**\\Windows\\WinSxS\\**", "**\\Windows\\Boot\\**"],
            Action = ItemAction.Block,
            Risk = RiskLevel.Critical,
            Priority = 100,
            Enabled = true
        });
        _rules.Add(new Rule
        {
            Id = "sys_drivers",
            Name = "System Drivers",
            PathPatterns = ["**\\Windows\\System32\\drivers\\**", "**\\Windows\\System32\\DriverStore\\**"],
            Action = ItemAction.Block,
            Risk = RiskLevel.Critical,
            Priority = 100,
            Enabled = true
        });
        _rules.Add(new Rule
        {
            Id = "sys_installer",
            Name = "Windows Installer",
            PathPatterns = ["**\\Windows\\Installer\\**"],
            Action = ItemAction.Block,
            Risk = RiskLevel.Critical,
            Priority = 99,
            Enabled = true
        });
        _rules.Add(new Rule
        {
            Id = "protect_docs",
            Name = "User Documents",
            PathPatterns = ["**\\Documents\\**", "**\\Desktop\\**"],
            Action = ItemAction.WarnDelete,
            Risk = RiskLevel.High,
            Priority = 90,
            Enabled = true
        });
        _rules.Add(new Rule
        {
            Id = "protect_db",
            Name = "Database Files",
            Extensions = [".db", ".sqlite", ".mdf", ".ldf", ".env"],
            Action = ItemAction.Block,
            Risk = RiskLevel.Critical,
            Priority = 95,
            Enabled = true
        });

        await Task.CompletedTask;
    }

    public List<Rule> GetRules(CleanLevel? level = null)
    {
        var filtered = level.HasValue
            ? _rules.Where(r => r.CleanLevel == level.Value || r.CleanLevel == CleanLevel.Custom)
            : _rules.AsEnumerable();
        return filtered.OrderByDescending(r => r.Priority).ToList();
    }

    public (ItemAction Action, RiskLevel Risk, string MatchedRule) Evaluate(
        string path, long sizeBytes = 0, DateTime? lastModified = null)
    {
        var bestRule = (Rule?)null;

        foreach (var rule in _rules.OrderByDescending(r => r.Priority))
        {
            if (!rule.Enabled) continue;
            if (!MatchesRule(rule, path, sizeBytes, lastModified)) continue;
            if (bestRule == null || rule.Priority > bestRule.Priority)
                bestRule = rule;
        }

        if (bestRule != null)
            return (bestRule.Action, bestRule.Risk, bestRule.Name);

        return (ItemAction.WarnDelete, RiskLevel.Medium, "Default");
    }

    private static bool MatchesRule(Rule rule, string path, long sizeBytes, DateTime? lastModified)
    {
        // Path pattern matching
        if (rule.PathPatterns.Count > 0)
        {
            bool pathMatch = false;
            foreach (var pattern in rule.PathPatterns)
            {
                if (MatchGlob(path, pattern)) { pathMatch = true; break; }
            }
            if (!pathMatch) return false;
        }

        // Extension matching
        if (rule.Extensions.Count > 0)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (!rule.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // Size check
        if (rule.MinSizeBytes > 0 && sizeBytes < rule.MinSizeBytes)
            return false;

        // Age check
        if (rule.MaxAgeDays > 0 && lastModified.HasValue)
        {
            var age = (DateTime.Now - lastModified.Value).TotalDays;
            if (age < rule.MaxAgeDays) return false;
        }

        return true;
    }

    private static bool MatchGlob(string path, string pattern)
    {
        // Simple glob matching: ** matches anything, * matches within path segment
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*\\*", ".*")
            .Replace("\\*", "[^\\\\]*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            path, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void AddRule(Rule rule)
    {
        _rules.RemoveAll(r => r.Id == rule.Id);
        _rules.Add(rule);
    }

    public bool RemoveRule(string ruleId)
    {
        return _rules.RemoveAll(r => r.Id == ruleId) > 0;
    }

    public async Task<bool> ToggleRule(string ruleId)
    {
        var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule == null) return false;
        rule.Enabled = !rule.Enabled;
        await SaveRulesAsync();
        return true;
    }

    public bool UpdateRule(string ruleId, Rule updated)
    {
        var idx = _rules.FindIndex(r => r.Id == ruleId);
        if (idx < 0) return false;
        _rules[idx] = updated;
        return true;
    }

    public async Task SaveRulesAsync()
    {
        // Save all rules back to combined JSON
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        var allPath = Path.Combine(_rulesDir, "all-rules.json");
        var json = JsonSerializer.Serialize(_rules, options);
        await File.WriteAllTextAsync(allPath, json);
    }
}
