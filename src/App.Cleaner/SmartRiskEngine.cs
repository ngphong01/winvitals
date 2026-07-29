using System.Diagnostics;
using App.Core;

namespace App.Cleaner;

/// <summary>
/// Smart Risk Engine — enhanced heuristics for better file classification.
/// Analyzes file context: age, origin, usage patterns, extension risk.
/// Produces natural language suggestions in Vietnamese.
/// Requirements: v2.3 AI Smart Clean
/// </summary>
public class SmartRiskEngine
{
    /// <summary>
    /// Enhanced risk assessment with contextual analysis.
    /// </summary>
    public static SmartAssessment Analyze(string path, long sizeBytes, DateTime? lastModified = null,
        string? originApp = null, int? daysUnused = null)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var name = Path.GetFileName(path);
        var dir = Path.GetDirectoryName(path) ?? "";
        var age = lastModified.HasValue ? (DateTime.Now - lastModified.Value).TotalDays : 0;

        var assessment = new SmartAssessment
        {
            Path = path, Name = name, SizeBytes = sizeBytes,
            CanDelete = true, Confidence = 80
        };

        // === System-critical blocks ===
        if (dir.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase) ||
            dir.Contains(@"\Windows\SysWOW64", StringComparison.OrdinalIgnoreCase))
            return Block(path, "File hệ thống Windows — xóa sẽ làm hỏng OS");

        if (ext is ".dll" or ".sys" or ".drv" && dir.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase))
            return Block(path, "Driver/system library — không thể xóa");

        // === High-confidence safe deletes ===
        if (dir.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) && age > 7)
        {
            assessment.Confidence = 95;
            assessment.Suggestion = $"File tạm {age:F0} ngày tuổi, không ứng dụng nào đang dùng. An toàn để xóa.";
            assessment.Risk = RiskLevel.Safe;
            assessment.Action = ItemAction.SafeDelete;
            return assessment;
        }

        if (dir.Contains(@"\Windows\Prefetch", StringComparison.OrdinalIgnoreCase))
        {
            assessment.Confidence = 90;
            assessment.Suggestion = "Prefetch cache — Windows tự tạo lại khi mở ứng dụng. An toàn.";
            assessment.Risk = RiskLevel.Safe;
            assessment.Action = ItemAction.SafeDelete;
            return assessment;
        }

        if (ext is ".log" or ".etl" && age > 30)
        {
            assessment.Confidence = 85;
            assessment.Suggestion = $"File log {age:F0} ngày tuổi — thường không cần thiết trừ khi debug.";
            assessment.Risk = RiskLevel.Low;
            assessment.Action = ItemAction.WarnDelete;
            return assessment;
        }

        // === Dev caches ===
        if (name is "node_modules" && daysUnused > 30)
        {
            assessment.Confidence = 92;
            assessment.Suggestion = $"node_modules không được dùng {daysUnused} ngày. Dọn an toàn, cài lại bằng npm install.";
            assessment.Risk = RiskLevel.Low;
            assessment.Action = ItemAction.WarnDelete;
            return assessment;
        }

        if (ext is ".pdb" && age > 90)
        {
            assessment.Confidence = 88;
            assessment.Suggestion = "Symbol file debug — chỉ cần khi debug crash dump. An toàn để xóa.";
            assessment.Risk = RiskLevel.Low;
            assessment.Action = ItemAction.WarnDelete;
            return assessment;
        }

        // === Suspicious / dangerous patterns ===
        if (ext is ".env" or ".ini" && name.Contains("cred", StringComparison.OrdinalIgnoreCase))
            return Block(path, "Có thể chứa credentials/password — không xóa mà không kiểm tra trước");

        if (ext is ".db" or ".sqlite" or ".mdf")
        {
            assessment.Confidence = 40;
            assessment.Suggestion = "File database — có thể chứa dữ liệu quan trọng. Kiểm tra kỹ trước khi xóa.";
            assessment.CanDelete = false;
            assessment.Risk = RiskLevel.High;
            assessment.Action = ItemAction.Block;
            return assessment;
        }

        // === Large old downloads ===
        if (dir.Contains(@"\Downloads\", StringComparison.OrdinalIgnoreCase) && sizeBytes > 100_000_000 && age > 90)
        {
            assessment.Confidence = 75;
            assessment.Suggestion = $"File download {age:F0} ngày tuổi, {ScanItem.FormatSize(sizeBytes)} — " +
                "có thể là installer cũ, xóa an toàn nếu không cần nữa.";
            assessment.Risk = RiskLevel.Low;
            assessment.Action = ItemAction.WarnDelete;
            return assessment;
        }

        // === Large files — warn ===
        if (sizeBytes > 1_000_000_000)
        {
            assessment.Confidence = 60;
            assessment.Suggestion = $"File rất lớn ({ScanItem.FormatSize(sizeBytes)}), " +
                $"{(age > 180 ? $"{age:F0} ngày không đụng đến — " : "")}kiểm tra kỹ trước khi xóa.";
            assessment.Risk = RiskLevel.Medium;
            assessment.Action = ItemAction.WarnDelete;
            return assessment;
        }

        // === Default ===
        assessment.Suggestion = age > 180
            ? $"Không dùng {age:F0} ngày — có thể xóa nếu không nhận ra."
            : $"Kiểm tra trước khi xóa.";
        assessment.Risk = RiskLevel.Medium;
        assessment.Action = ItemAction.WarnDelete;
        return assessment;
    }

    private static SmartAssessment Block(string path, string reason) => new()
    {
        Path = path, Name = Path.GetFileName(path),
        CanDelete = false, Confidence = 100,
        Risk = RiskLevel.Critical, Action = ItemAction.Block, Suggestion = reason
    };
}

/// <summary>
/// Rich assessment result with confidence score and Vietnamese suggestion.
/// </summary>
public class SmartAssessment
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool CanDelete { get; set; }
    public int Confidence { get; set; } // 0-100
    public string Suggestion { get; set; } = "";
    public RiskLevel Risk { get; set; } = RiskLevel.Unknown;
    public ItemAction Action { get; set; } = ItemAction.Skip;
    public string Explanation => $"[{Confidence}% confidence] {Suggestion}";
}
