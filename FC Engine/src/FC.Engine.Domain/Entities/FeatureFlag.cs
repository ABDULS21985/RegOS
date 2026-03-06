namespace FC.Engine.Domain.Entities;

public class FeatureFlag
{
    public int Id { get; set; }
    public string FlagCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int RolloutPercent { get; set; }
    public string? AllowedTenants { get; set; }
    public string? AllowedPlans { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
