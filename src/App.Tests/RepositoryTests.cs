using App.Core;
using App.Storage;
using App.Storage.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests;

/// <summary>
/// Integration tests for LiteDB repositories and UnitOfWork.
/// Validates Requirements 1.6, 1.7, 2.7
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDatabaseProvider _provider;
    private readonly ScanRepository _scanRepo;
    private readonly CleanRepository _cleanRepo;
    private readonly RuleRepository _ruleRepo;
    private readonly QuarantineRepository _quarantineRepo;
    private readonly PerformanceRepository _perfRepo;
    private readonly UnitOfWork _uow;

    public RepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"whm_test_{Guid.NewGuid():N}.db");
        _provider = new LiteDatabaseProvider(_testDbPath);
        var nullLogger = NullLoggerFactory.Instance;

        _scanRepo = new ScanRepository(_provider, new NullLogger<ScanRepository>());
        _cleanRepo = new CleanRepository(_provider, new NullLogger<CleanRepository>());
        _ruleRepo = new RuleRepository(_provider, new NullLogger<RuleRepository>());
        _quarantineRepo = new QuarantineRepository(_provider, new NullLogger<QuarantineRepository>());
        _perfRepo = new PerformanceRepository(_provider, new NullLogger<PerformanceRepository>());
        _uow = new UnitOfWork(_provider, new NullLogger<UnitOfWork>(),
            _scanRepo, _cleanRepo, _ruleRepo, _quarantineRepo, _perfRepo);
    }

    public void Dispose()
    {
        _uow.Dispose();
        _provider.Dispose();
        try { File.Delete(_testDbPath); } catch { }
    }

    // === Scan Repository (1.6) ===

    [Fact]
    public async Task ScanRepository_ShouldSaveAndRetrieveSession()
    {
        var session = new ScanSession
        {
            ScanType = ScanType.Quick,
            StartTime = DateTime.Now,
            DrivesScanned = ["C:"]
        };

        await _scanRepo.CreateAsync(session);
        session.Id.Should().BeGreaterThan(0);

        var sessions = await _scanRepo.GetRecentAsync(1);
        sessions.Should().ContainSingle(s => s.ScanType == ScanType.Quick);
    }

    // === Clean Repository (1.6) ===

    [Fact]
    public async Task CleanRepository_ShouldSaveAndRetrieveHistory()
    {
        var history = new CleanHistory
        {
            CleanDate = DateTime.Now,
            CleanLevel = CleanLevel.Quick,
            ItemsCleaned = 42,
            SpaceFreedBytes = 1_000_000
        };

        await _cleanRepo.CreateAsync(history);
        history.Id.Should().BeGreaterThan(0);

        var count = await _cleanRepo.GetTotalCleansAsync();
        count.Should().BeGreaterThan(0);
    }

    // === Rule Repository (1.6) ===

    [Fact]
    public async Task RuleRepository_ShouldManageRules()
    {
        var rule = new Rule
        {
            Id = "test_rule_001",
            Name = "Test Rule",
            Action = ItemAction.SafeDelete,
            Risk = RiskLevel.Safe,
            Priority = 80,
            Enabled = true
        };

        await _ruleRepo.CreateAsync(rule);

        var retrieved = await _ruleRepo.GetByIdAsync("test_rule_001");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Rule");
    }

    // === Quarantine Repository (1.6) ===

    [Fact]
    public async Task QuarantineRepository_ShouldTrackItemLifecycle()
    {
        var item = new QuarantineItem
        {
            OriginalPath = @"C:\test\file.txt",
            QuarantinePath = @"C:\quarantine\file.txt",
            FileName = "file.txt",
            SizeBytes = 1024,
            ExpiryDate = DateTime.Now.AddDays(14),
            Status = QuarantineStatus.Active
        };

        var created = await _quarantineRepo.CreateAsync(item);
        created.Id.Should().BeGreaterThan(0);

        var active = await _quarantineRepo.GetActiveAsync();
        active.Should().ContainSingle(i => i.FileName == "file.txt");

        // Verify active items count = 1
        active.Count.Should().Be(1);
    }

    // === Performance Repository (1.6) ===

    [Fact]
    public async Task PerformanceRepository_ShouldStoreAndQuerySnapshots()
    {
        var snap = new PerformanceSnapshot
        {
            CpuPercent = 45.5,
            MemoryPercent = 60.2,
            DiskPercent = 80.1,
            DriveLetter = "C",
            Timestamp = DateTime.Now
        };

        await _perfRepo.CreateAsync(snap);

        var latest = await _perfRepo.GetLatestAsync();
        latest.Should().NotBeNull();
        latest!.CpuPercent.Should().Be(45.5);

        var recent = await _perfRepo.GetRecentAsync(60);
        recent.Should().NotBeEmpty();
    }

    // === UnitOfWork Transaction (1.5) ===

    [Fact]
    public async Task UnitOfWork_ShouldSupportTransaction()
    {
        _uow.BeginTransaction();
        try
        {
            await _scanRepo.CreateAsync(new ScanSession { ScanType = ScanType.Quick });
            await _cleanRepo.CreateAsync(new CleanHistory { ItemsCleaned = 10 });
            _uow.Commit();
        }
        catch
        {
            _uow.Rollback();
            throw;
        }

        var count = await _cleanRepo.GetTotalCleansAsync();
        count.Should().BeGreaterThan(0);
    }
}
