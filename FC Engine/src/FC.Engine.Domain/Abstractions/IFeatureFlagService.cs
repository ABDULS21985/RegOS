namespace FC.Engine.Domain.Abstractions;

public interface IFeatureFlagService
{
    Task<bool> IsEnabled(string flagCode, Guid? tenantId = null);
}
