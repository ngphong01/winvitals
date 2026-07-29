using App.Storage.Repositories;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace App.Storage;

/// <summary>
/// Unit of Work — atomic transaction handling across multiple repositories.
/// Ensures consistency for multi-step operations like scan + clean + quarantine.
/// </summary>
public interface IUnitOfWork
{
    IScanRepository Scans { get; }
    ICleanRepository Cleans { get; }
    IRuleRepository Rules { get; }
    IQuarantineRepository Quarantine { get; }
    IPerformanceRepository Performance { get; }

    /// <summary>
    /// Begins a LiteDB transaction. All subsequent operations within this
    /// scope are atomic — either all succeed or all roll back.
    /// </summary>
    void BeginTransaction();

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    void Rollback();

    /// <summary>
    /// Ensures the database is cleanly disposed.
    /// </summary>
    void Dispose();
}

/// <summary>
/// Concrete UnitOfWork implementation wrapping LiteDB transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly LiteDatabaseProvider _provider;
    private readonly ILogger<UnitOfWork> _logger;
    private ILiteDatabase? _db;
    private bool _disposed;

    public IScanRepository Scans { get; }
    public ICleanRepository Cleans { get; }
    public IRuleRepository Rules { get; }
    public IQuarantineRepository Quarantine { get; }
    public IPerformanceRepository Performance { get; }

    public UnitOfWork(
        LiteDatabaseProvider provider,
        ILogger<UnitOfWork> logger,
        IScanRepository scans,
        ICleanRepository cleans,
        IRuleRepository rules,
        IQuarantineRepository quarantine,
        IPerformanceRepository performance)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Scans = scans ?? throw new ArgumentNullException(nameof(scans));
        Cleans = cleans ?? throw new ArgumentNullException(nameof(cleans));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Quarantine = quarantine ?? throw new ArgumentNullException(nameof(quarantine));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
    }

    public void BeginTransaction()
    {
        _db = _provider.Instance;
        _db.BeginTrans();
        _logger.LogDebug("UnitOfWork transaction started");
    }

    public void Commit()
    {
        _db?.Commit();
        _logger.LogDebug("UnitOfWork transaction committed");
    }

    public void Rollback()
    {
        _db?.Rollback();
        _logger.LogDebug("UnitOfWork transaction rolled back");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _db?.Dispose();
            _disposed = true;
        }
    }
}
