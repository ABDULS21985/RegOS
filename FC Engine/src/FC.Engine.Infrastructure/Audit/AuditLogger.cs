using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;

namespace FC.Engine.Infrastructure.Audit;

public class AuditLogger : IAuditLogger
{
    private readonly MetadataDbContext _db;

    public AuditLogger(MetadataDbContext db) => _db = db;

    public async Task Log(
        string entityType,
        int entityId,
        string action,
        object? oldValues,
        object? newValues,
        string performedBy,
        CancellationToken ct = default)
    {
        var entry = new AuditLogEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow
        };

        _db.AuditLog.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
