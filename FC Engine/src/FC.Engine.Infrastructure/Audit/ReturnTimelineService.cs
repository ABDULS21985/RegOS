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
            var evtType = MapAuditAction(entry.Action);
            events.Add(new TimelineEvent
            {
                Timestamp     = entry.PerformedAt,
                EventType     = evtType,
                Description   = BuildDescription(entry.Action, entry.EntityType),
                PerformedBy   = entry.PerformedBy,
                Diff          = BuildDiff(entry.OldValues, entry.NewValues),
                ActorInitials = GetInitials(entry.PerformedBy),
                ActorRole     = InferRole(entry.PerformedBy, evtType),
                EventCategory = ClassifyEvent(evtType, entry.PerformedBy),
            });
        }

        // Approval-related audit entries
        var approvalEntries = await _db.AuditLog
            .Where(a => a.EntityType == "SubmissionApproval" && a.EntityId == submissionId)
            .OrderBy(a => a.PerformedAt)
            .ToListAsync(ct);

        foreach (var entry in approvalEntries)
        {
            var evtType = MapAuditAction(entry.Action);
            events.Add(new TimelineEvent
            {
                Timestamp     = entry.PerformedAt,
                EventType     = evtType,
                Description   = BuildDescription(entry.Action, entry.EntityType),
                PerformedBy   = entry.PerformedBy,
                Diff          = BuildDiff(entry.OldValues, entry.NewValues),
                ActorInitials = GetInitials(entry.PerformedBy),
                ActorRole     = "Checker",
                EventCategory = ClassifyEvent(evtType, entry.PerformedBy),
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
                Timestamp     = change.ChangedAt,
                EventType     = "FieldChanged",
                Description   = $"Field '{change.FieldName}' changed via {change.ChangeSource}",
                PerformedBy   = change.ChangedBy,
                Diff = new Dictionary<string, object?>
                {
                    ["field"]  = change.FieldName,
                    ["before"] = change.OldValue,
                    ["after"]  = change.NewValue,
                },
                ChangedFields = [new FieldDiff(change.FieldName, change.OldValue, change.NewValue)],
                ActorInitials = GetInitials(change.ChangedBy),
                ActorRole     = "Maker",
                EventCategory = "UserAction",
            });
        }

        var sorted = events.OrderBy(e => e.Timestamp).ToList();

        EnrichWithDurations(sorted);
        MarkCurrentStatus(sorted);

        return sorted;
    }

    // ── Enrichment helpers ────────────────────────────────────────────────────

    private static readonly HashSet<string> StatusEvents =
    [
        "Submitted", "Validated", "Approved", "Rejected",
        "Created", "Amended", "Exported",
    ];

    private static void EnrichWithDurations(List<TimelineEvent> events)
    {
        // For each status event, compute how long the previous status lasted
        var statusEvents = events.Where(e => StatusEvents.Contains(e.EventType)).ToList();

        for (int i = 1; i < statusEvents.Count; i++)
        {
            var prev  = statusEvents[i - 1];
            var curr  = statusEvents[i];
            var span  = curr.Timestamp - prev.Timestamp;
            curr.StatusDuration = $"{FormatDuration(prev.EventType)}: {FormatTimeSpan(span)}";
        }
    }

    private static void MarkCurrentStatus(List<TimelineEvent> events)
    {
        // The last status-change event is the current status
        var last = events.LastOrDefault(e => StatusEvents.Contains(e.EventType));
        if (last is not null)
            last.IsCurrentStatus = true;
    }

    private static string FormatDuration(string priorEventType) => priorEventType switch
    {
        "Submitted" => "Awaiting Validation",
        "Validated" => "Under Review",
        "Approved"  => "Approved",
        "Rejected"  => "Returned",
        "Created"   => "Draft Phase",
        "Amended"   => "Re-review",
        _           => priorEventType,
    };

    private static string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalMinutes < 1) return "less than a minute";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalDays < 2) return $"1 day {span.Hours}h";
        return $"{(int)span.TotalDays} days {span.Hours}h";
    }

    private static string ClassifyEvent(string eventType, string performedBy)
    {
        if (performedBy is "System" or "Engine" or "Validation Engine" or "Auto")
            return "System";
        return eventType switch
        {
            "Validated" or "Exported" => "System",
            "FieldChanged"            => "UserAction",
            "Approved" or "Rejected"  => "UserAction",
            "Submitted" or "Amended"  => "UserAction",
            _                         => "System",
        };
    }

    private static string InferRole(string performedBy, string eventType) =>
        performedBy is "System" or "Engine" or "Validation Engine" or "Auto"
            ? "System"
            : eventType is "Approved" or "Rejected"
                ? "Checker"
                : "Maker";

    // ── Static helpers ────────────────────────────────────────────────────────

    internal static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        if (name is "System" or "Engine" or "Auto") return "SY";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][0].ToString().ToUpperInvariant()
            : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static string MapAuditAction(string action) => action switch
    {
        "Create"   => "Created",
        "Update"   => "Updated",
        "Submit"   => "Submitted",
        "Approve"  => "Approved",
        "Reject"   => "Rejected",
        "Validate" => "Validated",
        "Export"   => "Exported",
        "Delete"   => "Deleted",
        "Amend"    => "Amended",
        _          => action,
    };

    private static string BuildDescription(string action, string entityType)
    {
        var entity = entityType == "SubmissionApproval" ? "Approval" : "Submission";
        return action switch
        {
            "Create"   => $"{entity} created",
            "Update"   => $"{entity} updated",
            "Submit"   => "Submission submitted for processing",
            "Approve"  => "Submission approved by checker",
            "Reject"   => "Submission rejected — returned to maker",
            "Validate" => "Validation completed by engine",
            "Export"   => "Export generated",
            "Amend"    => "Submission amended and resubmitted",
            _          => $"{entity} — {action}",
        };
    }

    private static Dictionary<string, object?>? BuildDiff(string? oldValues, string? newValues)
    {
        if (oldValues == null && newValues == null) return null;

        var diff = new Dictionary<string, object?>();
        if (oldValues != null) diff["before"] = JsonSerializer.Deserialize<object>(oldValues);
        if (newValues != null) diff["after"]  = JsonSerializer.Deserialize<object>(newValues);
        return diff.Count > 0 ? diff : null;
    }
}
