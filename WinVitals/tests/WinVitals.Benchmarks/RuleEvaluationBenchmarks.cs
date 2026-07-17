using BenchmarkDotNet.Attributes;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;

namespace WinVitals.Benchmarks;

[MemoryDiagnoser]
public class RuleEvaluationBenchmarks
{
    private RuleEvaluator _evaluator = null!;
    private string[] _paths = null!;

    [GlobalSetup]
    public void Setup()
    {
        var repo = new RuleRepository(Path.GetTempPath());
        repo.SaveDefaults();
        _evaluator = new RuleEvaluator(repo.LoadAll().ToList());

        _paths = new string[10_000];
        for (int i = 0; i < _paths.Length; i++)
        {
            var dirs = new[] { "C:\\Users\\Test\\AppData\\Local\\Temp", "C:\\Windows\\Logs", "C:\\src\\bin\\Debug", "C:\\Users\\Test\\.git" };
            var files = new[] { "file.log", "temp.tmp", "cache.dat", "build.dll", "package.json" };
            _paths[i] = $"{dirs[i % dirs.Length]}\\{files[i % files.Length]}";
        }
    }

    [Benchmark]
    public int Evaluate10000Paths()
    {
        int matches = 0;
        foreach (var p in _paths)
        {
            var m = _evaluator.Evaluate(p, 1024, DateTime.UtcNow);
            if (m.Action != ItemAction.Block) matches++;
        }
        return matches;
    }
}
