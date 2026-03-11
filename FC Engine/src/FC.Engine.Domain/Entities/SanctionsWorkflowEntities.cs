namespace FC.Engine.Domain.Entities;

public class SanctionsFalsePositiveEntry
{
    public int Id { get; set; }
    public string MatchKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MatchedName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SanctionsDecisionAuditRecord
{
    public int Id { get; set; }
    public string MatchKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MatchedName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string PreviousDecision { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
