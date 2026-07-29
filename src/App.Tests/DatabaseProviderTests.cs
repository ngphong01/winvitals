using App.Storage;
using App.Core;
using FluentAssertions;

namespace App.Tests;

/// <summary>
/// Unit tests for DatabaseProvider
/// Validates Requirements 2.1, 2.4, 2.8
/// </summary>
public class DatabaseProviderTests : IDisposable
{
    private readonly string _testDbPath;
    private DatabaseProvider? _provider;

    public DatabaseProviderTests()
    {
        // Create a temporary test database path
        _testDbPath = Path.Combine(Path.GetTempPath(), $"whm_test_{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void DatabaseProvider_ShouldInitializeWithCustomPath()
    {
        // Arrange & Act
        _provider = new DatabaseProvider(_testDbPath);

        // Assert
        _provider.Should().NotBeNull();
        _provider.DatabasePath.Should().Be(_testDbPath);
        File.Exists(_testDbPath).Should().BeTrue("database file should be created");
    }

    [Fact]
    public void DatabaseProvider_ShouldInitializeWithDefaultPath()
    {
        // Arrange & Act
        _provider = new DatabaseProvider();

        // Assert
        _provider.Should().NotBeNull();
        _provider.DatabasePath.Should().NotBeNullOrEmpty();
        _provider.DatabasePath.Should().Contain("WindowsHealthManager", "should use app data folder");
        _provider.DatabasePath.Should().EndWith("whm.db", "should use default database name");
        File.Exists(_provider.DatabasePath).Should().BeTrue("database file should be created");
    }

    [Fact]
    public void GetDefaultDatabasePath_ShouldReturnAppDataLocation()
    {
        // Arrange & Act
        var defaultPath = DatabaseProvider.GetDefaultDatabasePath();

        // Assert
        defaultPath.Should().NotBeNullOrEmpty();
        defaultPath.Should().Contain(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        defaultPath.Should().Contain("WindowsHealthManager");
        defaultPath.Should().EndWith("whm.db");
    }

    [Fact]
    public async Task DatabaseProvider_ShouldSaveAndRetrieveScanSession()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        var session = new ScanSession
        {
            Id = 1,
            ScanType = ScanType.Quick,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddMinutes(5),
            TotalItemsFound = 100,
            TotalSizeBytes = 1024 * 1024,
            DrivesScanned = ["C:\\"]
        };

        // Act
        await _provider.SaveScanSessionAsync(session);
        var history = await _provider.GetScanHistoryAsync(30);

        // Assert
        history.Should().NotBeEmpty();
        history.Should().ContainSingle();
        var retrieved = history.First();
        retrieved.ScanType.Should().Be(ScanType.Quick);
        retrieved.TotalItemsFound.Should().Be(100);
        retrieved.TotalSizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public async Task DatabaseProvider_ShouldSaveAndRetrieveCleanHistory()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        var cleanHistory = new CleanHistory
        {
            Id = 1,
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Quick,
            ItemsCleaned = 50,
            SpaceFreedBytes = 512 * 1024,
            ItemsInQuarantine = 5
        };

        // Act
        await _provider.SaveCleanHistoryAsync(cleanHistory);
        var history = await _provider.GetCleanHistoryAsync(30);

        // Assert
        history.Should().NotBeEmpty();
        history.Should().ContainSingle();
        var retrieved = history.First();
        retrieved.CleanLevel.Should().Be(CleanLevel.Quick);
        retrieved.ItemsCleaned.Should().Be(50);
        retrieved.SpaceFreedBytes.Should().Be(512 * 1024);
    }

    [Fact]
    public async Task DatabaseProvider_ShouldSaveAndRetrieveQuarantineItem()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        var item = new QuarantineItem
        {
            Id = 1,
            OriginalPath = @"C:\Temp\test.txt",
            QuarantinePath = @"C:\Quarantine\test.txt",
            FileName = "test.txt",
            SizeBytes = 1024,
            QuarantineDate = DateTime.Now,
            Status = QuarantineStatus.Active,
            Reason = "Test quarantine",
            Risk = RiskLevel.Low
        };

        // Act
        await _provider.SaveQuarantineItemAsync(item);
        var items = await _provider.GetQuarantineItemsAsync();

        // Assert
        items.Should().NotBeEmpty();
        items.Should().ContainSingle();
        var retrieved = items.First();
        retrieved.FileName.Should().Be("test.txt");
        retrieved.Status.Should().Be(QuarantineStatus.Active);
        retrieved.Risk.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public async Task DatabaseProvider_ShouldUpdateQuarantineStatus()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        var item = new QuarantineItem
        {
            Id = 1,
            OriginalPath = @"C:\Temp\test.txt",
            QuarantinePath = @"C:\Quarantine\test.txt",
            FileName = "test.txt",
            SizeBytes = 1024,
            QuarantineDate = DateTime.Now,
            Status = QuarantineStatus.Active,
            Reason = "Test",
            Risk = RiskLevel.Low
        };

        await _provider.SaveQuarantineItemAsync(item);

        // Act
        var updated = await _provider.UpdateQuarantineStatusAsync(item.Id, QuarantineStatus.Restored);
        var items = await _provider.GetQuarantineItemsAsync();

        // Assert
        updated.Should().BeTrue();
        items.First().Status.Should().Be(QuarantineStatus.Restored);
    }

    [Fact]
    public async Task DatabaseProvider_ShouldRemoveQuarantineItem()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        var item = new QuarantineItem
        {
            Id = 1,
            OriginalPath = @"C:\Temp\test.txt",
            QuarantinePath = @"C:\Quarantine\test.txt",
            FileName = "test.txt",
            SizeBytes = 1024,
            QuarantineDate = DateTime.Now,
            Status = QuarantineStatus.Active,
            Reason = "Test",
            Risk = RiskLevel.Low
        };

        await _provider.SaveQuarantineItemAsync(item);

        // Act
        var removed = await _provider.RemoveQuarantineItemAsync(item.Id);
        var items = await _provider.GetQuarantineItemsAsync();

        // Assert
        removed.Should().BeTrue();
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task DatabaseProvider_ShouldSaveAndRetrieveSettings()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        const string key = "test_setting";
        const string value = "test_value";

        // Act
        await _provider.SetSettingAsync(key, value);
        var retrieved = await _provider.GetSettingAsync(key);

        // Assert
        retrieved.Should().Be(value);
    }

    [Fact]
    public async Task DatabaseProvider_ShouldGetStatistics()
    {
        // Arrange
        _provider = new DatabaseProvider(_testDbPath);
        
        // Add some test data
        await _provider.SaveScanSessionAsync(new ScanSession
        {
            Id = 1,
            ScanType = ScanType.Quick,
            StartTime = DateTime.Now,
            EndTime = DateTime.Now,
            TotalItemsFound = 100,
            TotalSizeBytes = 1024,
            DrivesScanned = ["C:\\"]
        });

        await _provider.SaveCleanHistoryAsync(new CleanHistory
        {
            Id = 1,
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Quick,
            ItemsCleaned = 10,
            SpaceFreedBytes = 512,
            ItemsInQuarantine = 2
        });

        // Act
        var stats = await _provider.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalScans.Should().Be(1);
        stats.TotalCleans.Should().Be(1);
        stats.TotalSpaceFreed.Should().Be(512);
    }

    [Fact]
    public void DatabaseProvider_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var testDir = Path.Combine(Path.GetTempPath(), $"whm_dir_test_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(testDir, "test.db");

        try
        {
            // Act
            using (var provider = new DatabaseProvider(dbPath))
            {
                // Assert
                Directory.Exists(testDir).Should().BeTrue("directory should be created");
                File.Exists(dbPath).Should().BeTrue("database file should be created");
            }
            
            // Force garbage collection and wait to ensure file handles are released
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }
        finally
        {
            // Cleanup - ensure database is disposed before deletion
            if (Directory.Exists(testDir))
            {
                try
                {
                    Directory.Delete(testDir, true);
                }
                catch
                {
                    // Ignore cleanup errors in test - file locks are not critical for test success
                }
            }
        }
    }

    public void Dispose()
    {
        _provider?.Dispose();
        
        // Clean up test database file
        if (File.Exists(_testDbPath))
        {
            try
            {
                File.Delete(_testDbPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up default database if created during tests
        try
        {
            var defaultPath = DatabaseProvider.GetDefaultDatabasePath();
            if (File.Exists(defaultPath))
            {
                File.Delete(defaultPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
