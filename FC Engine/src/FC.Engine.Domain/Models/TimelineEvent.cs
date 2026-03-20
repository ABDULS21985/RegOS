namespace FC.Engine.Domain.Models;

public class TimelineEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public Dictionary<string, object?>? Diff { get; set; }

    // Actor
    public string ActorInitials { get; set; } = "?";
    public string? ActorRole { get; set; }

    // Category for filtering
    // Values: "System" | "UserAction" | "Comment"
    public string EventCategory { get; set; } = "System";

    // Status duration — how long submission was in prior state before this event
    // e.g. "In Review: 2 days 4 hours"
    public string? StatusDuration { get; set; }

    // Marks the event representing the current status (last status-change event)
    public bool IsCurrentStatus { get; set; }

    // Comment attached to this event (checker approval/rejection note or submitter note)
    public string? Comment { get; set; }

    // Flat reply thread for compliance evidence
    public List<TimelineReply> Replies { get; set; } = [];

    // Document attachments (exports, uploads) linked to this event
    public List<TimelineAttachment> Attachments { get; set; } = [];

    // Richer field-level diff (populated for FieldChanged and Amendment events)
    public List<FieldDiff> ChangedFields { get; set; } = [];
}

public record FieldDiff(string FieldName, string? Before, string? After);

public record TimelineAttachment(
    string FileName,
    string DisplaySize,
    string? DownloadUrl,
    string FileType);

public record TimelineReply(
    int Id,
    string AuthorName,
    string AuthorInitials,
    string Content,
    DateTime CreatedAt);
