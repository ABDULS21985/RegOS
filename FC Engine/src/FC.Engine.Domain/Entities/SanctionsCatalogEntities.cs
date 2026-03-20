namespace FC.Engine.Domain.Entities;

public class SanctionsCatalogSourceRecord
{
    public int Id { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string RefreshCadence { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public DateTime MaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SanctionsCatalogEntryRecord
{
    public int Id { get; set; }
    public string EntryKey { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string PrimaryName { get; set; } = string.Empty;
    public string AliasesJson { get; set; } = "[]";
    public string Category { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime MaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
