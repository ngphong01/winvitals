using App.Core;
using App.Storage.Repositories;
using App.Storage.Services;
using Microsoft.Extensions.Logging;

namespace App.Cleaner;

/// <summary>
/// Manages quarantine operations — move, restore, expiry cleanup.
/// Requirements: 1.3, 3.4, 13.1, 13.2, 13.3, 13.5
/// </summary>
public class QuarantineService : IQuarantineService
{
    private readonly IQuarantineRepository _repository;
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger<QuarantineService> _logger;

    public QuarantineService(
        IQuarantineRepository repository,
        IFileSystemService fileSystem,
        ILogger<QuarantineService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> QuarantineAsync(IEnumerable<ScanItem> items,
        CancellationToken ct = default)
    {
        int count = 0;
        var quarantineBase = Path.Combine(AppContext.BaseDirectory, "quarantine");

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var destPath = await _fileSystem.MoveToQuarantineAsync(item.Path, quarantineBase, ct);

                var qi = new QuarantineItem
                {
                    OriginalPath = item.Path,
                    QuarantinePath = destPath,
                    FileName = item.Name,
                    SizeBytes = item.SizeBytes,
                    QuarantineDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(14),
                    Status = QuarantineStatus.Active,
                    Reason = item.Suggestion,
                    SourceModule = item.MatchedRule,
                    Risk = item.Risk
                };

                await _repository.CreateAsync(qi);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to quarantine: {Path}", item.Path);
            }
        }

        _logger.LogInformation("Quarantined {Count} items", count);
        return count;
    }

    public async Task<int> RestoreAsync(IEnumerable<int> itemIds,
        CancellationToken ct = default)
    {
        int count = 0;
        foreach (var id in itemIds)
        {
            if (ct.IsCancellationRequested) break;

            var item = await _repository.GetByIdAsync(id);
            if (item == null || item.Status != QuarantineStatus.Active) continue;

            var restored = await _fileSystem.RestoreFromQuarantineAsync(
                item.QuarantinePath, item.OriginalPath, ct);

            if (restored)
            {
                item.Status = QuarantineStatus.Restored;
                item.RestoreDate = DateTime.Now;
                await _repository.UpdateStatusAsync(id, QuarantineStatus.Restored);
                count++;
            }
        }

        _logger.LogInformation("Restored {Count} items from quarantine", count);
        return count;
    }

    public async Task<int> PermanentDeleteAsync(IEnumerable<int> itemIds,
        CancellationToken ct = default)
    {
        int count = 0;
        foreach (var id in itemIds)
        {
            if (ct.IsCancellationRequested) break;

            var item = await _repository.GetByIdAsync(id);
            if (item == null) continue;

            var deleted = await _fileSystem.SafeDeleteAsync(item.QuarantinePath, skipQuarantine: true, ct);
            if (deleted)
            {
                await _repository.UpdateStatusAsync(id, QuarantineStatus.Deleted);
                count++;
            }
        }

        _logger.LogInformation("Permanently deleted {Count} quarantine items", count);
        return count;
    }

    public async Task<List<QuarantineItem>> ListAsync(QuarantineStatus? status = null)
    {
        if (status.HasValue)
            return await _repository.GetByStatusAsync(status.Value);
        return await _repository.GetActiveAsync();
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken ct = default)
    {
        var expired = await _repository.GetExpiredAsync();
        int count = 0;

        foreach (var item in expired)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var deleted = await _fileSystem.SafeDeleteAsync(item.QuarantinePath, skipQuarantine: true, ct);
                if (deleted)
                {
                    await _repository.UpdateStatusAsync(item.Id, QuarantineStatus.Expired);
                    count++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup expired item: {Path}", item.QuarantinePath);
            }
        }

        if (count > 0)
            _logger.LogInformation("Cleaned up {Count} expired quarantine items", count);
        return count;
    }
}
