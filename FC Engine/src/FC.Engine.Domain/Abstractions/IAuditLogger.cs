namespace FC.Engine.Domain.Abstractions;

public interface IAuditLogger
{
    Task Log(string entityType, int entityId, string action, object? oldValues, object? newValues, string performedBy, CancellationToken ct = default);

    /// <summary>
    /// Overload for entities with non-integer PKs (e.g. Tenant Guid). Pass the Guid string as
    /// <paramref name="entityRef"/> and the tenant's own ID as <paramref name="explicitTenantId"/>
    /// so the row-level TenantId FK is set even in platform-admin context (no HTTP tenant context).
    /// </summary>
    Task Log(string entityType, string entityRef, string action, object? oldValues, object? newValues, string performedBy, Guid? explicitTenantId = null, CancellationToken ct = default);
}
