namespace FC.Engine.Domain.Abstractions;

public interface IInterModuleDataFlowEngine
{
    Task ProcessDataFlows(
        Guid tenantId,
        int submissionId,
        string sourceModuleCode,
        string sourceTemplateCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default);
}
