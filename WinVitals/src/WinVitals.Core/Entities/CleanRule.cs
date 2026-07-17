namespace WinVitals.Core.Entities;

public sealed class CleanRule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> PathPatterns { get; set; } = new();
    public List<string> Extensions { get; set; } = new();
    public long MinSizeBytes { get; set; }
    public int MaxAgeDays { get; set; }
    public ItemAction Action { get; set; } = ItemAction.WarnDelete;
    public RiskLevel Risk { get; set; } = RiskLevel.Medium;
    public int Priority { get; set; }
    public bool Enabled { get; set; } = true;
    public ScanPreset Preset { get; set; } = ScanPreset.Quick;
    public bool IsBuiltIn { get; set; }
}
