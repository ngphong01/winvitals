using System.Text.RegularExpressions;

namespace WinVitals.Core.Rules;

/// <summary>
/// Chuyển glob pattern → regex, cache lại để tránh compile lại.
/// Hỗ trợ: ** (bất kỳ path segments), * (bất kỳ char trừ separator), ? (1 char)
/// </summary>
public static class GlobMatcher
{
    private static readonly Dictionary<string, Regex> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public static bool IsMatch(string path, string pattern)
    {
        var regex = GetOrCreate(pattern);
        return regex.IsMatch(path);
    }

    public static bool IsMatchAny(string path, IEnumerable<string> patterns)
    {
        foreach (var p in patterns)
            if (IsMatch(path, p)) return true;
        return false;
    }

    private static Regex GetOrCreate(string pattern)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(pattern, out var cached)) return cached;
            var regex = new Regex(GlobToRegex(pattern),
                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
            _cache[pattern] = regex;
            return regex;
        }
    }

    private static string GlobToRegex(string glob)
    {
        // Normalize separators
        glob = glob.Replace('/', '\\');

        var sb = new System.Text.StringBuilder("^");
        int i = 0;
        while (i < glob.Length)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                // ** = match anything including separators
                sb.Append(".*");
                i += 2;
                // Skip trailing separator after **
                if (i < glob.Length && glob[i] == '\\') i++;
            }
            else if (c == '*')
            {
                sb.Append(@"[^\\]*");
                i++;
            }
            else if (c == '?')
            {
                sb.Append(@"[^\\]");
                i++;
            }
            else if (c == '\\')
            {
                sb.Append(@"\\");
                i++;
            }
            else if ("+()^$.{}[]|".Contains(c))
            {
                sb.Append('\\').Append(c);
                i++;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        sb.Append('$');
        return sb.ToString();
    }
}
