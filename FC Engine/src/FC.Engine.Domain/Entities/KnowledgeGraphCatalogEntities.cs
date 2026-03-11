namespace FC.Engine.Domain.Entities;

public class KnowledgeGraphNode
{
    public int Id { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? RegulatorCode { get; set; }
    public string? SourceReference { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime MaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeGraphEdge
{
    public int Id { get; set; }
    public string EdgeKey { get; set; } = string.Empty;
    public string EdgeType { get; set; } = string.Empty;
    public string SourceNodeKey { get; set; } = string.Empty;
    public string TargetNodeKey { get; set; } = string.Empty;
    public string? RegulatorCode { get; set; }
    public string? SourceReference { get; set; }
    public int Weight { get; set; } = 1;
    public string? MetadataJson { get; set; }
    public DateTime MaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
