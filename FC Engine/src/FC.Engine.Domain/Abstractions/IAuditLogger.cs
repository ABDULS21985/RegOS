namespace FC.Engine.Domain.Abstractions;

public interface IAuditLogger
{
    Task Log(string entityType, int entityId, string action, object? oldValues, object? newValues, string performedBy, CancellationToken ct = default);
}
