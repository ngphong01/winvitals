namespace WinVitals.Core.Entities;

public sealed class QuarantineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalPath { get; set; } = "";
    public string QuarantinePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime QuarantinedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public QuarantineStatus Status { get; set; } = QuarantineStatus.Active;
    public string Reason { get; set; } = "";
    public RiskLevel Risk { get; set; }
}
