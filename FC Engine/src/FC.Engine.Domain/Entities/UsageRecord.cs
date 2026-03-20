namespace FC.Engine.Domain.Entities;

public class UsageRecord
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly RecordDate { get; set; }
    public int ActiveUsers { get; set; }
    public int ActiveEntities { get; set; }
    public int ActiveModules { get; set; }
    public int ReturnsSubmitted { get; set; }
    public decimal StorageUsedMb { get; set; }
    public int ApiCallCount { get; set; }
}
