using System.Text.Json;

namespace FC.Engine.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Tier { get; set; }
    public int MaxModules { get; set; }
    public int MaxUsersPerEntity { get; set; }
    public int MaxEntities { get; set; }
    public int MaxApiCallsPerMonth { get; set; }
    public int MaxStorageMb { get; set; } = 500;
    public decimal BasePriceMonthly { get; set; }
    public decimal BasePriceAnnual { get; set; }
    public int TrialDays { get; set; } = 14;
    public string? Features { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanModulePricing> ModulePricing { get; set; } = new List<PlanModulePricing>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public List<string> GetFeatures()
    {
        if (string.IsNullOrWhiteSpace(Features))
            return new List<string>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(Features);
            if (parsed is { Count: > 0 })
            {
                return parsed
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch
        {
            // Fall through to CSV support for backward compatibility.
        }

        return Features
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool HasFeature(string code)
    {
        var features = GetFeatures();
        return features.Contains(code, StringComparer.OrdinalIgnoreCase)
               || features.Contains("all_features", StringComparer.OrdinalIgnoreCase);
    }
}
