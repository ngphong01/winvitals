using App.Core;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace App.Storage.Repositories;

/// <summary>
/// Concrete implementation of IQuarantineRepository using LiteDB
/// </summary>
public class QuarantineRepository : IQuarantineRepository
{
    private readonly LiteDatabaseProvider _database;
    private readonly ILogger<QuarantineRepository> _logger;

    public QuarantineRepository(LiteDatabaseProvider database, ILogger<QuarantineRepository> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new quarantine item
    /// </summary>
    /// <param name="item">The quarantine item to create</param>
    /// <returns>The created quarantine item with assigned ID</returns>
    public async Task<QuarantineItem> CreateAsync(QuarantineItem item)
    {
        try
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            // Validate item data
            ValidateQuarantineItem(item);

            await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var doc = ToDocument(item);
                collection.Insert(doc);
                item.Id = ParseId(doc.Id);
            });

            _logger.LogInformation("Created quarantine item {ItemId} for '{FileName}' from '{OriginalPath}'", 
                item.Id, item.FileName, item.OriginalPath);

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create quarantine item for '{FileName}'", item?.FileName ?? "Unknown");
            throw;
        }
    }

    /// <summary>
    /// Gets a quarantine item by ID
    /// </summary>
    /// <param name="id">The ID of the quarantine item</param>
    /// <returns>The quarantine item if found, null otherwise</returns>
    public async Task<QuarantineItem?> GetByIdAsync(int id)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var objectId = CreateObjectId(id);
                var doc = collection.FindById(objectId);
                
                if (doc == null)
                {
                    _logger.LogDebug("Quarantine item {ItemId} not found", id);
                    return null;
                }

                return ToEntity(doc);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get quarantine item {ItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets all active quarantine items
    /// </summary>
    /// <returns>List of active quarantine items</returns>
    public async Task<List<QuarantineItem>> GetActiveAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                
                var docs = collection.Find(x => x.Status == QuarantineStatus.Active.ToString())
                    .OrderByDescending(x => x.QuarantineDate)
                    .ToList();

                _logger.LogDebug("Found {Count} active quarantine items", docs.Count);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active quarantine items");
            throw;
        }
    }

    /// <summary>
    /// Gets all expired quarantine items
    /// </summary>
    /// <returns>List of expired quarantine items</returns>
    public async Task<List<QuarantineItem>> GetExpiredAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var now = DateTime.Now;
                
                var docs = collection.Find(x => 
                    x.Status == QuarantineStatus.Active.ToString() && x.ExpiryDate < now)
                    .OrderByDescending(x => x.QuarantineDate)
                    .ToList();

                _logger.LogDebug("Found {Count} expired quarantine items", docs.Count);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get expired quarantine items");
            throw;
        }
    }

    /// <summary>
    /// Gets quarantine items by status
    /// </summary>
    /// <param name="status">The status to filter by</param>
    /// <returns>List of quarantine items with the specified status</returns>
    public async Task<List<QuarantineItem>> GetByStatusAsync(QuarantineStatus status)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                
                var docs = collection.Find(x => x.Status == status.ToString())
                    .OrderByDescending(x => x.QuarantineDate)
                    .ToList();

                _logger.LogDebug("Found {Count} quarantine items with status {Status}", docs.Count, status);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get quarantine items by status {Status}", status);
            throw;
        }
    }

    /// <summary>
    /// Updates the status of a quarantine item
    /// </summary>
    /// <param name="id">The ID of the item to update</param>
    /// <param name="status">The new status</param>
    /// <returns>True if update succeeded, false otherwise</returns>
    public async Task<bool> UpdateStatusAsync(int id, QuarantineStatus status)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var objectId = CreateObjectId(id);
                var doc = collection.FindById(objectId);
                
                if (doc == null)
                {
                    _logger.LogWarning("Quarantine item {ItemId} not found for status update", id);
                    return false;
                }

                var oldStatus = doc.Status;
                doc.Status = status.ToString();
                
                // Update relevant timestamps
                switch (status)
                {
                    case QuarantineStatus.Restored:
                        doc.RestoreDate = DateTime.Now;
                        break;
                    case QuarantineStatus.Deleted:
                    case QuarantineStatus.Expired:
                        // Keep restore date if item was restored before being deleted
                        break;
                }

                var result = collection.Update(doc);

                if (result)
                {
                    _logger.LogInformation("Updated quarantine item {ItemId} status from {OldStatus} to {NewStatus}", 
                        id, oldStatus, status);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update quarantine item {ItemId} status to {Status}", id, status);
            throw;
        }
    }

    /// <summary>
    /// Restores a quarantine item (updates status and restore date)
    /// </summary>
    /// <param name="id">The ID of the item to restore</param>
    /// <returns>True if restore succeeded, false otherwise</returns>
    public async Task<bool> RestoreAsync(int id)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var objectId = CreateObjectId(id);
                var doc = collection.FindById(objectId);
                
                if (doc == null)
                {
                    _logger.LogWarning("Quarantine item {ItemId} not found for restore", id);
                    return false;
                }

                if (doc.Status != QuarantineStatus.Active.ToString())
                {
                    _logger.LogWarning("Cannot restore quarantine item {ItemId} with status {Status}", id, doc.Status);
                    return false;
                }

                doc.Status = QuarantineStatus.Restored.ToString();
                doc.RestoreDate = DateTime.Now;
                
                var result = collection.Update(doc);

                if (result)
                {
                    _logger.LogInformation("Restored quarantine item {ItemId} '{FileName}' to '{OriginalPath}'", 
                        id, doc.FileName, doc.OriginalPath);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore quarantine item {ItemId}", id);
            throw;
        }
    }

    /// <summary>
    /// Deletes a quarantine item permanently
    /// </summary>
    /// <param name="id">The ID of the item to delete</param>
    /// <returns>True if deletion succeeded, false otherwise</returns>
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<QuarantineItemDoc>("quarantine");
                var objectId = CreateObjectId(id);
                
                // Get the item details before deletion for logging
                var doc = collection.FindById(objectId);
                if (doc == null)
                {
                    _logger.LogWarning("Quarantine item {ItemId} not found for deletion", id);
                    return false;
                }

                var result = collection.Delete(objectId);

                if (result)
                {
                    _logger.LogInformation("Permanently deleted quarantine item {ItemId} '{FileName}'", 
                        id, doc.FileName);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete quarantine item {ItemId}", id);
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Validates quarantine item data
    /// </summary>
    private static void ValidateQuarantineItem(QuarantineItem item)
    {
        if (string.IsNullOrWhiteSpace(item.OriginalPath))
            throw new ArgumentException("Original path cannot be null or empty", nameof(item));

        if (string.IsNullOrWhiteSpace(item.FileName))
            throw new ArgumentException("File name cannot be null or empty", nameof(item));

        if (item.QuarantineDate == default)
            item.QuarantineDate = DateTime.Now;

        if (item.ExpiryDate == default)
            item.ExpiryDate = DateTime.Now.AddDays(14); // Default 14 days
    }

    /// <summary>
    /// Converts a QuarantineItem entity to a document for storage
    /// </summary>
    private static QuarantineItemDoc ToDocument(QuarantineItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        return new QuarantineItemDoc
        {
            OriginalPath = item.OriginalPath,
            QuarantinePath = item.QuarantinePath,
            FileName = item.FileName,
            SizeBytes = item.SizeBytes,
            QuarantineDate = item.QuarantineDate,
            RestoreDate = item.RestoreDate,
            ExpiryDate = item.ExpiryDate,
            Status = item.Status.ToString(),
            Reason = item.Reason,
            SourceModule = item.SourceModule,
            Risk = item.Risk.ToString()
        };
    }

    /// <summary>
    /// Converts a document to a QuarantineItem entity
    /// </summary>
    private static QuarantineItem ToEntity(QuarantineItemDoc doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        return new QuarantineItem
        {
            Id = ParseId(doc.Id),
            OriginalPath = doc.OriginalPath,
            QuarantinePath = doc.QuarantinePath,
            FileName = doc.FileName,
            SizeBytes = doc.SizeBytes,
            QuarantineDate = doc.QuarantineDate,
            RestoreDate = doc.RestoreDate,
            ExpiryDate = doc.ExpiryDate,
            Status = Enum.Parse<QuarantineStatus>(doc.Status),
            Reason = doc.Reason,
            SourceModule = doc.SourceModule,
            Risk = Enum.Parse<RiskLevel>(doc.Risk)
        };
    }

    /// <summary>
    /// Creates an ObjectId from an integer ID
    /// </summary>
    private static ObjectId CreateObjectId(int id)
    {
        return new ObjectId(id.ToString("X8").PadLeft(24, '0'));
    }

    /// <summary>
    /// Parses an integer ID from an ObjectId
    /// </summary>
    private static int ParseId(ObjectId objectId)
    {
        var hexString = objectId.ToString().Substring(0, Math.Min(8, objectId.ToString().Length));
        return int.Parse(hexString.PadLeft(8, '0'), System.Globalization.NumberStyles.HexNumber);
    }

    #endregion

    #region Document Classes

    /// <summary>
    /// Document class for QuarantineItem storage in LiteDB
    /// </summary>
    private class QuarantineItemDoc
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public string OriginalPath { get; set; } = string.Empty;
        public string QuarantinePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime QuarantineDate { get; set; }
        public DateTime? RestoreDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string SourceModule { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
    }

    #endregion
}