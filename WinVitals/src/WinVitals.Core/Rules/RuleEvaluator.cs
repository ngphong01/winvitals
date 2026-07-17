using WinVitals.Core.Entities;

namespace WinVitals.Core.Rules;

public sealed record RuleMatch(ItemAction Action, RiskLevel Risk, string RuleId, string RuleName);

/// <summary>
/// Pure evaluator – không có state, dễ test.
/// Nhận rules từ ngoài (đã merged built-in + JSON), evaluate 1 file/folder.
/// </summary>
public sealed class RuleEvaluator
{
    private readonly IReadOnlyList<CleanRule> _rulesByPriority;

    public int RuleCount => _rulesByPriority.Count;

    public RuleEvaluator(IEnumerable<CleanRule> rules)
    {
        _rulesByPriority = rules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.Priority)
            .ToList();
    }

    /// <summary>
    /// Trả về rule match đầu tiên (priority cao nhất) hoặc fallback.
    /// </summary>
    public RuleMatch Evaluate(string path, long sizeBytes, DateTime lastModifiedUtc)
    {
        var ext = Path.GetExtension(path);
        var now = DateTime.UtcNow;

        foreach (var rule in _rulesByPriority)
        {
            if (!Matches(rule, path, ext, sizeBytes, lastModifiedUtc, now)) continue;
            return new RuleMatch(rule.Action, rule.Risk, rule.Id, rule.Name);
        }

        // Default fallback – không xóa gì mà không được đánh giá
        return new RuleMatch(ItemAction.WarnDelete, RiskLevel.Medium, "default", "Default");
    }

    private static bool Matches(CleanRule rule, string path, string ext,
        long sizeBytes, DateTime lastModifiedUtc, DateTime now)
    {
        var matchedPattern = rule.PathPatterns.Count == 0 ||
            GlobMatcher.IsMatchAny(path, rule.PathPatterns);

        var matchedExt = rule.Extensions.Count == 0 ||
            rule.Extensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));

        // PathPatterns và Extensions: OR logic (match 1 trong 2 là đủ)
        // Nhưng nếu cả 2 đều được chỉ định thì ít nhất 1 phải match
        if (rule.PathPatterns.Count > 0 && rule.Extensions.Count > 0)
        {
            if (!matchedPattern && !matchedExt) return false;
        }
        else
        {
            if (!matchedPattern) return false;
            if (!matchedExt) return false;
        }

        // Size
        if (rule.MinSizeBytes > 0 && sizeBytes < rule.MinSizeBytes) return false;

        // Age
        if (rule.MaxAgeDays > 0)
        {
            var age = (now - lastModifiedUtc).TotalDays;
            if (age < rule.MaxAgeDays) return false;
        }

        // Nếu rule không có bất kỳ điều kiện nào thì coi như không match
        // (tránh rule rỗng nuốt hết mọi file)
        if (rule.PathPatterns.Count == 0 && rule.Extensions.Count == 0 &&
            rule.MinSizeBytes == 0 && rule.MaxAgeDays == 0)
            return false;

        return true;
    }
}
