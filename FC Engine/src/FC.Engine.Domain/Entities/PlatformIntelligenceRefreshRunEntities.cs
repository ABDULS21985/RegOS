namespace FC.Engine.Domain.Entities;

public class PlatformIntelligenceRefreshRunRecord
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureMessage { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public int DurationMilliseconds { get; set; }
    public int InstitutionCount { get; set; }
    public int InterventionCount { get; set; }
    public int TimelineCount { get; set; }
    public int DashboardPacksMaterialized { get; set; }
    public DateTime? RolloutCatalogMaterializedAt { get; set; }
    public DateTime? KnowledgeCatalogMaterializedAt { get; set; }
    public DateTime? KnowledgeDossierMaterializedAt { get; set; }
    public DateTime? CapitalPackMaterializedAt { get; set; }
    public DateTime? SanctionsPackMaterializedAt { get; set; }
    public DateTime? SanctionsStrDraftCatalogMaterializedAt { get; set; }
    public DateTime? ResiliencePackMaterializedAt { get; set; }
    public DateTime? ModelRiskPackMaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
