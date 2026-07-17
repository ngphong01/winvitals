namespace App.Core;

/// <summary>
/// Các loại quét
/// </summary>
public enum ScanType
{
    Quick,
    Deep,
    Developer,
    Performance
}

/// <summary>
/// Mức độ làm sạch
/// </summary>
public enum CleanLevel
{
    Quick,      // Temp, Recycle Bin, Logs, Crash dumps
    Deep,       // Windows Update cache, App leftovers, Old installers
    Developer,  // node_modules, dist, build, .next, .gradle...
    Custom      // User-defined
}

/// <summary>
/// Mức độ rủi ro khi xóa
/// </summary>
public enum RiskLevel
{
    Safe,       // Có thể xóa an toàn
    Low,        // Rủi ro thấp
    Medium,     // Cần kiểm tra
    High,       // Cảnh báo mạnh
    Critical,   // Tuyệt đối không xóa
    Unknown     // Chưa xác định
}

/// <summary>
/// Hành động với item
/// </summary>
public enum ItemAction
{
    SafeDelete,     // Xóa an toàn, không cần confirm
    WarnDelete,     // Cảnh báo trước khi xóa
    Block,          // Không bao giờ xóa
    Quarantine,     // Chuyển vào quarantine
    Skip            // Bỏ qua
}

/// <summary>
/// Loại item trong kết quả scan
/// </summary>
public enum ItemCategory
{
    TempFile,
    LogFile,
    CrashDump,
    WindowsUpdateCache,
    RecycleBin,
    BrowserCache,
    OldInstaller,
    LargeFile,
    DuplicateFile,
    OrphanFile,
    DevCache,
    Prefetch,
    ThumbnailCache,
    Unknown
}

/// <summary>
/// Trạng thái của item trong quarantine
/// </summary>
public enum QuarantineStatus
{
    Active,         // Đang trong quarantine
    Restored,       // Đã khôi phục
    Deleted,        // Đã xóa vĩnh viễn
    Expired         // Hết hạn, chờ xóa
}
