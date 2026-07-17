using WinVitals.Core.Entities;

namespace WinVitals.Core.Rules;

/// <summary>
/// Built-in rules không thể bị xóa/override bởi user.
/// Priority 170-200 đảm bảo luôn thắng JSON rules.
/// </summary>
public static class BuiltInRules
{
    public static IReadOnlyList<CleanRule> All { get; } = new List<CleanRule>
    {
        new()
        {
            Id = "sys_windows_core", Priority = 200, IsBuiltIn = true,
            Name = "Windows Core (System32/SysWOW64/WinSxS/Boot)",
            PathPatterns = { @"**\Windows\System32\**", @"**\Windows\SysWOW64\**",
                             @"**\Windows\WinSxS\**", @"**\Windows\Boot\**" },
            Action = ItemAction.Block, Risk = RiskLevel.Critical
        },
        new()
        {
            Id = "sys_drivers", Priority = 200, IsBuiltIn = true,
            Name = "Drivers & DriverStore",
            PathPatterns = { @"**\drivers\**", @"**\DriverStore\**" },
            Action = ItemAction.Block, Risk = RiskLevel.Critical
        },
        new()
        {
            Id = "sys_installer", Priority = 195, IsBuiltIn = true,
            Name = "Windows Installer",
            PathPatterns = { @"**\Windows\Installer\**" },
            Action = ItemAction.Block, Risk = RiskLevel.High
        },
        new()
        {
            Id = "protect_db", Priority = 190, IsBuiltIn = true,
            Name = "Database files",
            Extensions = { ".db", ".sqlite", ".sqlite3", ".mdf", ".ldf", ".ndf" },
            Action = ItemAction.Block, Risk = RiskLevel.Critical
        },
        new()
        {
            Id = "protect_secrets", Priority = 190, IsBuiltIn = true,
            Name = "Secrets & VCS",
            PathPatterns = { @"**\.git\**", @"**\.svn\**", @"**\.hg\**" },
            Extensions = { ".env", ".pem", ".key", ".pfx" },
            Action = ItemAction.Block, Risk = RiskLevel.Critical
        },
        new()
        {
            Id = "protect_user_docs", Priority = 180, IsBuiltIn = true,
            Name = "User Documents & Desktop",
            PathPatterns = { @"**\Users\*\Documents\**", @"**\Users\*\Desktop\**",
                             @"**\Users\*\Pictures\**", @"**\Users\*\Videos\**" },
            Action = ItemAction.WarnDelete, Risk = RiskLevel.High
        },
        new()
        {
            Id = "protect_program_files", Priority = 170, IsBuiltIn = true,
            Name = "Program Files",
            PathPatterns = { @"**\Program Files\**", @"**\Program Files (x86)\**" },
            Action = ItemAction.WarnDelete, Risk = RiskLevel.High
        }
    };
}
