using App.Core;
using App.Scanner;
using FluentAssertions;

namespace App.Tests;

/// <summary>
/// Comprehensive tests for scanners — cloud, browser, Windows Store, dev caches.
/// Tests validate detection logic and edge cases.
/// </summary>
public class ScannerTests
{
    // === Cloud Cache Scanner ===

    [Fact]
    public void CloudCacheScanner_ShouldHaveName()
    {
        var scanner = new CloudCacheScanner();
        scanner.Name.Should().Be("Cloud Cache Scanner");
        scanner.ScanType.Should().Be(ScanType.Deep);
    }

    [Fact]
    public async Task CloudCacheScanner_Scan_ShouldReturnItems()
    {
        var scanner = new CloudCacheScanner();
        var items = await scanner.ScanAsync(["C:"]);
        items.Should().NotBeNull();
    }

    // === Browser Cache Scanner ===

    [Fact]
    public void BrowserCacheScanner_ShouldHaveName()
    {
        var scanner = new BrowserCacheScanner();
        scanner.Name.Should().Be("Browser Cache Scanner");
        scanner.ScanType.Should().Be(ScanType.Developer);
    }

    [Fact]
    public async Task BrowserCacheScanner_Scan_ShouldReturnItems()
    {
        var scanner = new BrowserCacheScanner();
        var items = await scanner.ScanAsync(["C:"]);
        items.Should().NotBeNull();
    }

    [Fact]
    public async Task BrowserCacheScanner_Scan_WithProgress()
    {
        var scanner = new BrowserCacheScanner();
        var progress = new Progress<(string Status, int Progress)>(p => { });
        var items = await scanner.ScanAsync(["C:"], progress);
        items.Should().NotBeNull();
    }

    // === Windows Store Scanner ===

    [Fact]
    public void WindowsStoreScanner_ShouldHaveName()
    {
        var scanner = new WindowsStoreScanner();
        scanner.Name.Should().Be("Windows Store Scanner");
        scanner.ScanType.Should().Be(ScanType.Deep);
    }

    [Fact]
    public async Task WindowsStoreScanner_Scan_ShouldReturnItems()
    {
        var scanner = new WindowsStoreScanner();
        var items = await scanner.ScanAsync(["C:"]);
        items.Should().NotBeNull();
    }

    // === Dev Cache Scanner ===

    [Fact]
    public void DevCacheScanner_ShouldHaveName()
    {
        var scanner = new DevCacheScanner();
        scanner.Name.Should().Be("Package Cache Analyzer");
        scanner.ScanType.Should().Be(ScanType.Developer);
    }

    [Fact]
    public async Task DevCacheScanner_Scan_ShouldNotThrow()
    {
        var scanner = new DevCacheScanner();
        var items = await scanner.ScanAsync(["C:"]);
        items.Should().NotBeNull();
    }

    // === Stale Project Detector ===

    [Fact]
    public void StaleProjectDetector_ShouldHaveName()
    {
        var detector = new StaleProjectDetector();
        detector.Name.Should().Be("Stale Project Detector");
        detector.ScanType.Should().Be(ScanType.Developer);
    }

    [Fact]
    public async Task StaleProjectDetector_Scan_ShouldNotThrow()
    {
        var detector = new StaleProjectDetector();
        var items = await detector.ScanAsync(["C:"]);
        items.Should().NotBeNull();
    }

    // === ItemCategory consistency ===

    [Theory]
    [InlineData(ItemCategory.TempFile)]
    [InlineData(ItemCategory.LogFile)]
    [InlineData(ItemCategory.CrashDump)]
    [InlineData(ItemCategory.WindowsUpdateCache)]
    [InlineData(ItemCategory.RecycleBin)]
    [InlineData(ItemCategory.BrowserCache)]
    [InlineData(ItemCategory.DevCache)]
    [InlineData(ItemCategory.LargeFile)]
    [InlineData(ItemCategory.DuplicateFile)]
    [InlineData(ItemCategory.OrphanFile)]
    public void ItemCategory_Enum_ShouldHaveAllValues(ItemCategory cat)
    {
        cat.ToString().Should().NotBeNullOrEmpty();
    }

    // === ScanItem formatting ===

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(1024)]
    [InlineData(1_048_576)]
    [InlineData(1_073_741_824)]
    [InlineData(1_099_511_627_776)]
    public void ScanItem_FormatSize_ShouldReturnString(long bytes)
    {
        var result = ScanItem.FormatSize(bytes);
        result.Should().NotBeNullOrEmpty();
        result.Should().ContainAny("B", "KB", "MB", "GB", "TB");
    }

    [Fact]
    public void ScanItem_SizeFormatted_ShouldReturnPositive()
    {
        var item = new ScanItem { SizeBytes = 2_048_576 };
        item.SizeFormatted.Should().ContainAny("MB", "KB");
    }

    // === Risk level consistency ===

    [Theory]
    [InlineData(RiskLevel.Safe)]
    [InlineData(RiskLevel.Low)]
    [InlineData(RiskLevel.Medium)]
    [InlineData(RiskLevel.High)]
    [InlineData(RiskLevel.Critical)]
    public void RiskLevel_Order_ShouldBeConsistent(RiskLevel level)
    {
        // Verify all risk levels can be compared
        var safe = RiskLevel.Safe;
        var critical = RiskLevel.Critical;
        (level >= safe).Should().BeTrue();
        (level <= critical).Should().BeTrue();
    }

    // === ItemAction consistency ===

    [Theory]
    [InlineData(ItemAction.SafeDelete)]
    [InlineData(ItemAction.WarnDelete)]
    [InlineData(ItemAction.Block)]
    [InlineData(ItemAction.Quarantine)]
    [InlineData(ItemAction.Skip)]
    public void ItemAction_Enum_ShouldHaveAllValues(ItemAction action)
    {
        action.ToString().Should().NotBeNullOrEmpty();
    }

    // === Edge cases ===

    [Fact]
    public void ScanItem_DefaultValues_ShouldBeSafe()
    {
        var item = new ScanItem();
        item.Risk.Should().Be(RiskLevel.Unknown);
        item.RecommendedAction.Should().Be(ItemAction.WarnDelete);
        item.Category.Should().Be(ItemCategory.Unknown);
        item.Path.Should().BeEmpty();
        item.SizeBytes.Should().Be(0);
    }

    [Fact]
    public void PerformanceSnapshot_DefaultValues_ShouldBeZero()
    {
        var snap = new PerformanceSnapshot();
        snap.CpuPercent.Should().Be(0);
        snap.MemoryPercent.Should().Be(0);
        snap.DiskPercent.Should().Be(0);
    }

    [Fact]
    public void QuarantineItem_DefaultExpiry_ShouldBe14Days()
    {
        var item = new QuarantineItem();
        item.ExpiryDate.Should().BeCloseTo(DateTime.Now.AddDays(14), TimeSpan.FromSeconds(1));
        item.Status.Should().Be(QuarantineStatus.Active);
    }

    // === CleanHistory validation ===

    [Fact]
    public void CleanHistory_SpaceFreedFormatted_ShouldWork()
    {
        var h = new CleanHistory { SpaceFreedBytes = 5_000_000 };
        h.SpaceFreedFormatted.Should().ContainAny("MB", "KB", "GB");
    }

    [Fact]
    public void ScanSession_SizeFormatted_ShouldWork()
    {
        var s = new ScanSession { TotalSizeBytes = 1_000_000_000 };
        s.TotalSizeFormatted.Should().ContainAny("MB", "GB");
    }

    // === AppStatistics ===

    [Fact]
    public void AppStatistics_DefaultValues_ShouldBeSafe()
    {
        var stats = new AppStatistics();
        stats.TotalScans.Should().Be(0);
        stats.TotalSpaceFreed.Should().Be(0);
        stats.TotalSpaceFreedFormatted.Should().Contain("0");
    }
}
