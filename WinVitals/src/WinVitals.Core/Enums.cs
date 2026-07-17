namespace WinVitals.Core;

public enum RiskLevel
{
    Safe,
    Low,
    Medium,
    High,
    Critical
}

public enum ItemAction
{
    SafeDelete,
    WarnDelete,
    Quarantine,
    Block
}

public enum ItemCategory
{
    TempFile,
    BrowserCache,
    Prefetch,
    ThumbnailCache,
    LogFile,
    CrashDump,
    WindowsUpdateCache,
    OldInstaller,
    LargeFile,
    DuplicateFile,
    DevCache,
    OrphanFile,
    Unknown
}

public enum ScanPreset
{
    Quick,
    Deep,
    Developer
}

public enum QuarantineStatus
{
    Active,
    Restored,
    Purged,
    Expired
}
