namespace FC.Engine.Domain.Entities;

public class MigrationModuleSignOff
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int ModuleId { get; set; }
    public bool IsSignedOff { get; set; }
    public int SignedOffBy { get; set; }
    public DateTime SignedOffAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
