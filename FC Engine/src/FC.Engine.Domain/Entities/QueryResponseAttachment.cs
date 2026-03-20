namespace FC.Engine.Domain.Entities;

/// <summary>File attached to a query response before dispatch to regulator.</summary>
public class QueryResponseAttachment
{
    public long Id { get; set; }
    public long QueryResponseId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string BlobStoragePath { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;           // SHA-512 hex
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public QueryResponse? QueryResponse { get; set; }
}
