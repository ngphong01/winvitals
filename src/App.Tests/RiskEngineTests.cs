using App.Core;
using App.Cleaner;
using FluentAssertions;
using Xunit;

namespace App.Tests;

/// <summary>
/// CRITICAL: Verify protected paths are NEVER marked safe to delete
/// </summary>
public class RiskEngineTests
{
    private static string CreateTempRulesDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"whm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "protected-paths.json"), """
        [
            {"path": "C:\\Windows\\System32", "reason": "System files", "risk": "Critical"},
            {"path": "C:\\Windows\\SysWOW64", "reason": "32-bit system", "risk": "Critical"},
            {"path": "C:\\Windows\\WinSxS", "reason": "Component store", "risk": "Critical"},
            {"path": "C:\\Windows\\Installer", "reason": "Installer DB", "risk": "Critical"},
            {"path": "C:\\Windows\\System32\\drivers", "reason": "Drivers", "risk": "Critical"},
            {"path": "C:\\Windows\\System32\\DriverStore", "reason": "Driver Store", "risk": "Critical"},
            {"path": "C:\\Windows\\Boot", "reason": "Boot config", "risk": "Critical"},
            {"path": "C:\\Windows\\Fonts", "reason": "System fonts", "risk": "Critical"},
            {"path": "C:\\Windows\\INF", "reason": "Driver INF files", "risk": "Critical"},
            {"path": "C:\\Program Files", "reason": "Installed apps", "risk": "High"},
            {"path": "C:\\Program Files (x86)", "reason": "32-bit apps", "risk": "High"},
            {"path": "C:\\ProgramData\\Microsoft", "reason": "MS app data", "risk": "High"},
            {"path": "*.db", "reason": "Database files", "risk": "Critical"},
            {"path": "*.sqlite", "reason": "SQLite files", "risk": "Critical"},
            {"path": ".env", "reason": "Env config", "risk": "Critical"},
            {"path": ".git", "reason": "Git repo", "risk": "Critical"},
            {"path": "**\\Documents\\**", "reason": "User docs", "risk": "Critical"},
            {"path": "**\\Desktop\\**", "reason": "User desktop", "risk": "Critical"},
            {"path": "**\\Downloads\\**", "reason": "User downloads", "risk": "High"}
        ]
        """);
        return dir;
    }

    [Fact]
    public void System32_MustBe_Protected()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        engine.IsProtected(@"C:\Windows\System32\kernel32.dll").Should().BeTrue();
        engine.IsProtected(@"C:\Windows\System32\drivers\etc.sys").Should().BeTrue();
        engine.IsProtected(@"C:\Windows\SysWOW64\notepad.exe").Should().BeTrue();
        engine.IsProtected(@"C:\Windows\WinSxS\amd64_foo").Should().BeTrue();
        engine.IsProtected(@"C:\Windows\Installer\{GUID}\setup.msi").Should().BeTrue();
    }

    [Fact]
    public void DatabaseFiles_MustBe_Protected()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        engine.IsProtected(@"C:\projects\myapp\data.db").Should().BeTrue();
        engine.IsProtected(@"C:\projects\myapp\production.sqlite").Should().BeTrue();
        engine.IsProtected(@"C:\projects\myapp\.env").Should().BeTrue();
    }

    [Fact]
    public void UserFolders_MustBe_Protected()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        engine.IsProtected(@"C:\Users\me\Documents\tax.docx").Should().BeTrue();
        engine.IsProtected(@"C:\Users\me\Desktop\notes.txt").Should().BeTrue();
        engine.IsProtected(@"C:\Users\me\Desktop\project\.git\HEAD").Should().BeTrue();
    }

    [Fact]
    public void NormalFiles_AreNot_Protected()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        engine.IsProtected(@"C:\Temp\log.txt").Should().BeFalse();
        engine.IsProtected(@"D:\Games\setup.exe").Should().BeFalse();
        engine.IsProtected(@"C:\Users\me\.npmrc").Should().BeFalse();
    }

    [Fact]
    public void AssessRisk_ProtectedPath_ReturnsCritical()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        var risk = engine.AssessRisk(@"C:\Windows\System32\file.dll", ItemCategory.Unknown);
        risk.Should().Be(RiskLevel.Critical);
    }

    [Fact]
    public void AssessRisk_TempFile_ReturnsSafe()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        var risk = engine.AssessRisk(@"C:\Users\me\AppData\Local\Temp\tmp123.tmp",
            ItemCategory.TempFile);
        risk.Should().Be(RiskLevel.Safe);
    }

    [Fact]
    public void AssessRisk_OrphanFile_ReturnsHigh()
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        var risk = engine.AssessRisk(@"D:\OrphanData\OldApp\data.bin",
            ItemCategory.OrphanFile);
        risk.Should().Be(RiskLevel.High);
    }

    /// <summary>
    /// Golden test: verify ALL 20+ protected paths from rules/protected-paths.json
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData(@"C:\Windows\SysWOW64\kernel32.dll")]
    [InlineData(@"C:\Windows\WinSxS\manifest.xml")]
    [InlineData(@"C:\Windows\System32\drivers\network.sys")]
    [InlineData(@"C:\Windows\System32\config\SAM")]
    [InlineData(@"C:\Windows\Installer\abc123.msi")]
    [InlineData(@"C:\Windows\System32\DriverStore\oem0.inf")]
    [InlineData(@"C:\Windows\Boot\BCD")]
    [InlineData(@"C:\Windows\Fonts\arial.ttf")]
    [InlineData(@"C:\Windows\INF\usb.inf")]
    [InlineData(@"C:\Program Files\SomeApp\app.exe")]
    [InlineData(@"C:\Program Files (x86)\OldApp\dll.dll")]
    [InlineData(@"C:\ProgramData\Microsoft\Crypto\RSA\data")]
    [InlineData(@"C:\Users\test\Documents\secret.txt")]
    [InlineData(@"C:\Users\test\Desktop\shortcut.lnk")]
    [InlineData(@"C:\Users\test\Downloads\installer.msi")]
    [InlineData(@"C:\projects\code\.env")]
    [InlineData(@"C:\projects\code\.git\config")]
    [InlineData(@"C:\projects\code\mydb.db")]
    [InlineData(@"C:\projects\code\app.sqlite")]
    public void AllProtectedPaths_MustReturn_True(string path)
    {
        using var tmp = new TempDir(CreateTempRulesDir());
        var engine = new RiskEngine(tmp.Path);

        engine.IsProtected(path).Should().BeTrue(
            $"Path '{path}' MUST be protected — it's in the protected-paths.json");
    }
}

/// <summary>
/// Helper: auto-deletes temp dir on dispose
/// </summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }
    public TempDir(string path) => Path = path;
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { }
    }
}
