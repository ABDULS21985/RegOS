namespace FC.Engine.Domain.Entities;

/// <summary>
/// Defines a subscription plan that controls module access, quotas, and features.
/// Plans are seeded as system reference data (Starter, Professional, Enterprise, Group).
/// </summary>
public class SubscriptionPlan
{
    public int Id { get; set; }

    /// <summary>Unique code: STARTER, PROFESSIONAL, ENTERPRISE, GROUP.</summary>
    public string PlanCode { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Maximum institutions allowed under this plan.</summary>
    public int MaxInstitutions { get; set; } = 1;

    /// <summary>Maximum users per institution entity.</summary>
    public int MaxUsersPerEntity { get; set; } = 10;

    /// <summary>Maximum number of regulatory modules that can be activated.</summary>
    public int MaxModules { get; set; } = 1;

    /// <summary>Whether this plan grants access to all eligible modules automatically.</summary>
    public bool AllModulesIncluded { get; set; }

    /// <summary>Comma-separated feature flags (e.g., "xml_submission,validation,reporting,api_access,white_label").</summary>
    public string Features { get; set; } = "xml_submission,validation,reporting";

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
