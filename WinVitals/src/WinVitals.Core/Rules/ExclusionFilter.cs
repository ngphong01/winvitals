namespace WinVitals.Core.Rules;

/// <summary>
/// Filter cứng chạy TRƯỚC rule evaluator. Nếu path match exclusion → không bao giờ trả về.
/// </summary>
public sealed class ExclusionFilter
{
    private readonly IReadOnlyList<string> _patterns;

    public ExclusionFilter(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();
    }

    public bool IsExcluded(string path) =>
        _patterns.Count > 0 && GlobMatcher.IsMatchAny(path, _patterns);

    public static ExclusionFilter Empty { get; } = new(Array.Empty<string>());
}
