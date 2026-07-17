using WinVitals.Core;

namespace WinVitals.Core.Entities;

public sealed class CleanSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public ScanPreset Preset { get; set; }
    public bool WasScheduled { get; set; }

    public int TotalItems { get; set; }
    public int QuarantinedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public long BytesFreed { get; set; }

    public Dictionary<ItemCategory, long> BytesByCategory { get; set; } = new();
    public Dictionary<ItemCategory, int> CountByCategory { get; set; } = new();

    public TimeSpan Elapsed => CompletedAtUtc - StartedAtUtc;
}
