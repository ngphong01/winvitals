using App.Core;
using LiteDB;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace App.Storage.Repositories;

/// <summary>
/// Concrete implementation of IRuleRepository using LiteDB
/// </summary>
public class RuleRepository : IRuleRepository
{
    private readonly LiteDatabaseProvider _database;
    private readonly ILogger<RuleRepository> _logger;

    public RuleRepository(LiteDatabaseProvider database, ILogger<RuleRepository> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new rule
    /// </summary>
    /// <param name="rule">The rule to create</param>
    /// <returns>The created rule with assigned ID</returns>
    public async Task<Rule> CreateAsync(Rule rule)
    {
        try
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            // Validate rule data
            ValidateRule(rule);

            await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                
                // Generate ID if not provided
                if (string.IsNullOrEmpty(rule.Id))
                {
                    rule.Id = Guid.NewGuid().ToString("N")[..12];
                }

                var doc = ToDocument(rule);
                collection.Insert(doc);
            });

            _logger.LogInformation("Created rule {RuleId} '{RuleName}' for level {CleanLevel}", 
                rule.Id, rule.Name, rule.CleanLevel);

            return rule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create rule '{RuleName}'", rule?.Name ?? "Unknown");
            throw;
        }
    }

    /// <summary>
    /// Gets a rule by ID
    /// </summary>
    /// <param name="id">The ID of the rule</param>
    /// <returns>The rule if found, null otherwise</returns>
    public async Task<Rule?> GetByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                var doc = collection.FindById(id);
                
                if (doc == null)
                {
                    _logger.LogDebug("Rule {RuleId} not found", id);
                    return null;
                }

                return ToEntity(doc);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rule {RuleId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets all rules
    /// </summary>
    /// <returns>List of all rules</returns>
    public async Task<List<Rule>> GetAllAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                
                var docs = collection.FindAll()
                    .OrderBy(x => x.Priority)
                    .ThenBy(x => x.Name)
                    .ToList();

                _logger.LogDebug("Found {Count} total rules", docs.Count);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all rules");
            throw;
        }
    }

    /// <summary>
    /// Gets rules by clean level
    /// </summary>
    /// <param name="level">The clean level to filter by</param>
    /// <returns>List of rules for the specified level</returns>
    public async Task<List<Rule>> GetByLevelAsync(CleanLevel level)
    {
        try
        {
            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                
                var docs = collection.Find(x => x.CleanLevel == level.ToString() && x.Enabled)
                    .OrderBy(x => x.Priority)
                    .ThenBy(x => x.Name)
                    .ToList();

                _logger.LogDebug("Found {Count} enabled rules for level {CleanLevel}", docs.Count, level);

                return docs.Select(ToEntity).ToList();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rules for level {CleanLevel}", level);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing rule
    /// </summary>
    /// <param name="rule">The rule with updated data</param>
    /// <returns>True if update succeeded, false otherwise</returns>
    public async Task<bool> UpdateAsync(Rule rule)
    {
        try
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (string.IsNullOrEmpty(rule.Id))
                throw new ArgumentException("Rule ID cannot be null or empty", nameof(rule));

            // Validate rule data
            ValidateRule(rule);

            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                var doc = ToDocument(rule);
                var result = collection.Update(doc);

                if (result)
                {
                    _logger.LogInformation("Updated rule {RuleId} '{RuleName}'", rule.Id, rule.Name);
                }
                else
                {
                    _logger.LogWarning("Rule {RuleId} not found for update", rule.Id);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update rule {RuleId} '{RuleName}'", rule?.Id, rule?.Name);
            throw;
        }
    }

    /// <summary>
    /// Deletes a rule
    /// </summary>
    /// <param name="id">The ID of the rule to delete</param>
    /// <returns>True if deletion succeeded, false otherwise</returns>
    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                var result = collection.Delete(id);

                if (result)
                {
                    _logger.LogInformation("Deleted rule {RuleId}", id);
                }
                else
                {
                    _logger.LogWarning("Rule {RuleId} not found for deletion", id);
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete rule {RuleId}", id);
            throw;
        }
    }

    /// <summary>
    /// Toggles a rule's enabled state
    /// </summary>
    /// <param name="id">The ID of the rule to toggle</param>
    /// <returns>True if toggle succeeded, false otherwise</returns>
    public async Task<bool> ToggleAsync(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return await Task.Run(() =>
            {
                var collection = _database.Instance.GetCollection<RuleDoc>("rules");
                var doc = collection.FindById(id);
                
                if (doc == null)
                {
                    _logger.LogWarning("Rule {RuleId} not found for toggle", id);
                    return false;
                }

                doc.Enabled = !doc.Enabled;
                var result = collection.Update(doc);

                if (result)
                {
                    _logger.LogInformation("Toggled rule {RuleId} to {Enabled}", id, doc.Enabled ? "enabled" : "disabled");
                }

                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle rule {RuleId}", id);
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Validates rule data
    /// </summary>
    private static void ValidateRule(Rule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
            throw new ArgumentException("Rule name cannot be null or empty", nameof(rule));

        if (rule.PathPatterns == null)
            rule.PathPatterns = [];

        if (rule.Extensions == null)
            rule.Extensions = [];

        if (rule.Priority < 0 || rule.Priority > 100)
            throw new ArgumentException("Rule priority must be between 0 and 100", nameof(rule));
    }

    /// <summary>
    /// Converts a Rule entity to a document for storage
    /// </summary>
    private static RuleDoc ToDocument(Rule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        return new RuleDoc
        {
            Id = rule.Id,
            Name = rule.Name,
            Description = rule.Description,
            PathPatterns = System.Text.Json.JsonSerializer.Serialize(rule.PathPatterns ?? []),
            Extensions = System.Text.Json.JsonSerializer.Serialize(rule.Extensions ?? []),
            MinSizeBytes = rule.MinSizeBytes,
            MaxAgeDays = rule.MaxAgeDays,
            Action = rule.Action.ToString(),
            Risk = rule.Risk.ToString(),
            Priority = rule.Priority,
            Enabled = rule.Enabled,
            CleanLevel = rule.CleanLevel.ToString()
        };
    }

    /// <summary>
    /// Converts a document to a Rule entity
    /// </summary>
    private static Rule ToEntity(RuleDoc doc)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc));

        List<string> pathPatterns = [];
        List<string> extensions = [];

        try
        {
            pathPatterns = System.Text.Json.JsonSerializer.Deserialize<List<string>>(doc.PathPatterns) ?? [];
        }
        catch (JsonException)
        {
            // Fallback to comma-separated values for backward compatibility
            pathPatterns = doc.PathPatterns.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        try
        {
            extensions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(doc.Extensions) ?? [];
        }
        catch (JsonException)
        {
            // Fallback to comma-separated values for backward compatibility
            extensions = doc.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        return new Rule
        {
            Id = doc.Id,
            Name = doc.Name,
            Description = doc.Description,
            PathPatterns = pathPatterns,
            Extensions = extensions,
            MinSizeBytes = doc.MinSizeBytes,
            MaxAgeDays = doc.MaxAgeDays,
            Action = Enum.Parse<ItemAction>(doc.Action),
            Risk = Enum.Parse<RiskLevel>(doc.Risk),
            Priority = doc.Priority,
            Enabled = doc.Enabled,
            CleanLevel = Enum.Parse<CleanLevel>(doc.CleanLevel)
        };
    }

    #endregion

    #region Document Classes

    /// <summary>
    /// Document class for Rule storage in LiteDB
    /// </summary>
    private class RuleDoc
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PathPatterns { get; set; } = string.Empty;
        public string Extensions { get; set; } = string.Empty;
        public long MinSizeBytes { get; set; }
        public int MaxAgeDays { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public int Priority { get; set; } = 50;
        public bool Enabled { get; set; } = true;
        public string CleanLevel { get; set; } = string.Empty;
    }

    #endregion
}