namespace FC.Engine.Domain.Entities;

public class SanctionsScreeningRunRecord
{
    public int Id { get; set; }
    public string ScreeningKey { get; set; } = string.Empty;
    public double ThresholdPercent { get; set; }
    public DateTime ScreenedAt { get; set; }
    public int TotalSubjects { get; set; }
    public int MatchCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SanctionsScreeningResultRecord
{
    public int Id { get; set; }
    public string ScreeningKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Disposition { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string MatchedName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SanctionsTransactionCheckRecord
{
    public int Id { get; set; }
    public string TransactionKey { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public double ThresholdPercent { get; set; }
    public bool HighRisk { get; set; }
    public string ControlDecision { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public bool RequiresStrDraft { get; set; }
    public DateTime ScreenedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SanctionsTransactionPartyResultRecord
{
    public int Id { get; set; }
    public string TransactionKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string PartyRole { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string Disposition { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public string MatchedName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
