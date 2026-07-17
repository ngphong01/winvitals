using BenchmarkDotNet.Attributes;
using WinVitals.Core.Rules;

namespace WinVitals.Benchmarks;

[MemoryDiagnoser]
public class GlobMatcherBenchmarks
{
    private string[] _paths = null!;

    [GlobalSetup]
    public void Setup()
    {
        _paths = new[] {
            "C:\\Users\\Test\\AppData\\Local\\Temp\\file.tmp",
            "C:\\Windows\\Logs\\CBS\\cbs.log",
            "C:\\src\\bin\\Debug\\net8.0\\app.dll",
            "C:\\Users\\Test\\.git\\objects\\pack\\pack.idx",
            "C:\\node_modules\\lodash\\index.js"
        };
    }

    [Benchmark]
    public bool[] MatchCached()
    {
        var results = new bool[5];
        string[] patterns = { "**\\Temp\\**", "**\\Logs\\**", "**\\bin\\Debug\\**", "**\\.git\\**", "**\\node_modules\\**" };
        for (int i = 0; i < 5; i++)
            results[i] = GlobMatcher.IsMatch(_paths[i], patterns[i]);
        return results;
    }
}
