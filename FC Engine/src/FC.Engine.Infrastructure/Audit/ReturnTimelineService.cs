using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Audit;

public class ReturnTimelineService : IReturnTimelineService
{
    private readonly MetadataDbContext _db;

    public ReturnTimelineService(MetadataDbContext db) => _db = db;

    public async Task<List<TimelineEvent>> GetTimelineAsync(int submissionId, CancellationToken ct = default)
    {
        var events = new List<TimelineEvent>();

        // Audit log entries for this submission
        var auditEntries = await _db.AuditLog
            .Where(a => a.EntityType == "Submission" && a.EntityId == submissionId)
            .OrderBy(a => a.PerformedAt)
            .ToListAsync(ct);

        foreach (var entry in auditEntries)
        {
            events.Add(new TimelineEvent
            {
                Timestamp = entry.PerformedAt,
                EventType = MapAuditAction(entry.Action),
                Description = BuildDescription(entry.Action, entry.EntityType),
                PerformedBy = entry.PerformedBy,
                Diff = BuildDiff(entry.OldValues, entry.NewValues)
            });
        }

        // Approval-related audit entries
        var approvalEntries = await _db.AuditLog
            .Where(a => a.EntityType == "SubmissionApproval" && a.EntityId == submissionId)
            .OrderBy(a => a.PerformedAt)
            .ToListAsync(ct);

        foreach (var entry in approvalEntries)
        {
            events.Add(new TimelineEvent
            {
                Timestamp = entry.PerformedAt,
                EventType = MapAuditAction(entry.Action),
                Description = BuildDescription(entry.Action, entry.EntityType),
                PerformedBy = entry.PerformedBy,
                Diff = BuildDiff(entry.OldValues, entry.NewValues)
            });
        }

        // Field change history entries
        var fieldChanges = await _db.FieldChangeHistory
            .Where(f => f.SubmissionId == submissionId)
            .OrderBy(f => f.ChangedAt)
            .ToListAsync(ct);

        foreach (var change in fieldChanges)
        {
            events.Add(new TimelineEvent
            {
                Timestamp = change.ChangedAt,
                EventType = "FieldChanged",
                Description = $"Field '{change.FieldName}' changed via {change.ChangeSource}",
                PerformedBy = change.ChangedBy,
                Diff = new Dictionary<string, object?>
                {
                    ["field"] = change.FieldName,
                    ["before"] = change.OldValue,
                    ["after"] = change.NewValue
                }
            });
        }

        return events.OrderBy(e => e.Timestamp).ToList();
    }

    private static string MapAuditAction(string action) => action switch
    {
        "Create" => "Created",
        "Update" => "Updated",
        "Submit" => "Submitted",
        "Approve" => "Approved",
        "Reject" => "Rejected",
        "Validate" => "Validated",
        "Export" => "Exported",
        "Delete" => "Deleted",
        _ => action
    };

    private static string BuildDescription(string action, string entityType)
    {
        var entity = entityType == "SubmissionApproval" ? "Approval" : "Submission";
        return action switch
        {
            "Create" => $"{entity} created",
            "Update" => $"{entity} updated",
            "Submit" => "Submission submitted for processing",
            "Approve" => "Submission approved",
            "Reject" => "Submission rejected",
            "Validate" => "Validation completed",
            "Export" => "Export generated",
            _ => $"{entity} — {action}"
        };
    }

    private static Dictionary<string, object?>? BuildDiff(string? oldValues, string? newValues)
    {
        if (oldValues == null && newValues == null) return null;

        var diff = new Dictionary<string, object?>();
        if (oldValues != null) diff["before"] = JsonSerializer.Deserialize<object>(oldValues);
        if (newValues != null) diff["after"] = JsonSerializer.Deserialize<object>(newValues);
        return diff.Count > 0 ? diff : null;
    }
}
