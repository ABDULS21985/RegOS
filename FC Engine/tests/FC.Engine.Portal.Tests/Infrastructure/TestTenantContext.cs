using FC.Engine.Domain.Abstractions;

namespace FC.Engine.Portal.Tests.Infrastructure;

internal sealed class TestTenantContext : ITenantContext
{
    public Guid? CurrentTenantId { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public Guid? ImpersonatingTenantId { get; init; }
}
