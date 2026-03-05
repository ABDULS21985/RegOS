namespace FC.Engine.Domain.Entities;

public class DataFeedRequestLog
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string ReturnCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
