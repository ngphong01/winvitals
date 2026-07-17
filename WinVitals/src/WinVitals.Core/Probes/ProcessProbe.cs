using System.Diagnostics;

namespace WinVitals.Core.Probes;

/// <summary>
/// Enumerate processes. Đo CPU% bằng cách delta CPU time giữa 2 lần đọc,
/// KHÔNG dùng PerformanceCounter per-process vì scale tệ khi có >100 process.
/// </summary>
public sealed class ProcessProbe
{
    private readonly Dictionary<int, (TimeSpan cpu, DateTime at)> _prev = new();
    private readonly int _cpuCount = Environment.ProcessorCount;

    public IReadOnlyList<Entities.ProcessSnapshot> Snapshot()
    {
        var procs = Process.GetProcesses();
        var now = DateTime.UtcNow;
        var result = new List<Entities.ProcessSnapshot>(procs.Length);
        var seen = new HashSet<int>(procs.Length);

        foreach (var p in procs)
        {
            seen.Add(p.Id);
            try
            {
                TimeSpan cpu;
                try { cpu = p.TotalProcessorTime; }
                catch { continue; }

                double cpuPct = 0;
                if (_prev.TryGetValue(p.Id, out var prev))
                {
                    var deltaCpu = (cpu - prev.cpu).TotalMilliseconds;
                    var deltaWall = (now - prev.at).TotalMilliseconds;
                    if (deltaWall > 0)
                        cpuPct = Math.Round(deltaCpu / (deltaWall * _cpuCount) * 100.0, 1);
                }
                _prev[p.Id] = (cpu, now);

                string? path = null;
                string? publisher = null;
                string? desc = null;
                bool isSystem = false;
                try
                {
                    var mm = p.MainModule;
                    if (mm is not null)
                    {
                        path = mm.FileName;
                        desc = mm.FileVersionInfo.FileDescription;
                        publisher = mm.FileVersionInfo.CompanyName;
                    }
                }
                catch { isSystem = true; }

                var snap = new Entities.ProcessSnapshot(
                    Pid: p.Id,
                    Name: SafeName(p),
                    Description: string.IsNullOrWhiteSpace(desc) ? null : desc,
                    Publisher: string.IsNullOrWhiteSpace(publisher) ? null : publisher,
                    WorkingSetBytes: SafeLong(() => p.WorkingSet64),
                    CpuPercent: Math.Clamp(cpuPct, 0, 100 * _cpuCount),
                    ThreadCount: SafeInt(() => p.Threads.Count),
                    StartTimeUtc: SafeDate(() => p.StartTime.ToUniversalTime()),
                    ExecutablePath: path,
                    IsSystem: isSystem,
                    IsElevated: false);

                result.Add(snap);
            }
            catch { }
            finally { p.Dispose(); }
        }

        foreach (var k in _prev.Keys.Where(k => !seen.Contains(k)).ToList())
            _prev.Remove(k);

        return result;
    }

    private static string SafeName(Process p) { try { return p.ProcessName; } catch { return "?"; } }
    private static long SafeLong(Func<long> f) { try { return f(); } catch { return 0; } }
    private static int SafeInt(Func<int> f) { try { return f(); } catch { return 0; } }
    private static DateTime SafeDate(Func<DateTime> f) { try { return f(); } catch { return DateTime.MinValue; } }

    public static void Kill(int pid)
    {
        using var p = Process.GetProcessById(pid);
        p.Kill(entireProcessTree: true);
    }
}
