namespace FC.Engine.Domain.Abstractions;

public interface ICarryForwardService
{
    Task<CarryForwardResult> GetCarryForwardValues(
        Guid tenantId,
        string returnCode,
        int currentPeriodId,
        CancellationToken ct = default);
}

public class CarryForwardResult
{
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int? SourceSubmissionId { get; set; }
    public int? SourceReturnPeriodId { get; set; }
    public string? SourcePeriodLabel { get; set; }
    public bool HasValues => Values.Count > 0;
}
