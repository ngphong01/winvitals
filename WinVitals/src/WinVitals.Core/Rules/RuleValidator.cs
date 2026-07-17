using System.Text.RegularExpressions;
using WinVitals.Core.Entities;

namespace WinVitals.Core.Rules;

public static class RuleValidator
{
    public static IReadOnlyList<string> Validate(CleanRule rule)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Id))
            errors.Add("Id is required.");
        else if (!Regex.IsMatch(rule.Id, @"^[a-z0-9_]+$"))
            errors.Add("Id must contain only lowercase letters, digits and underscores.");

        if (string.IsNullOrWhiteSpace(rule.Name))
            errors.Add("Name is required.");

        if (rule.Priority < 0 || rule.Priority > 199)
            errors.Add("Priority must be between 0 and 199 (200+ reserved for built-in).");

        if (rule.MinSizeBytes < 0)
            errors.Add("Min size cannot be negative.");

        if (rule.MaxAgeDays < 0)
            errors.Add("Max age cannot be negative.");

        var hasCondition = rule.PathPatterns.Count > 0
                        || rule.Extensions.Count > 0
                        || rule.MinSizeBytes > 0
                        || rule.MaxAgeDays > 0;
        if (!hasCondition)
            errors.Add("Rule must have at least one condition (path, extension, size, or age).");

        foreach (var ext in rule.Extensions)
        {
            if (!ext.StartsWith('.'))
                errors.Add($"Extension '{ext}' must start with a dot (e.g. '.log').");
        }

        if (rule.Action == ItemAction.Block && rule.Risk == RiskLevel.Safe)
            errors.Add("A Block rule shouldn't have Safe risk – use High or Critical.");

        return errors;
    }
}
