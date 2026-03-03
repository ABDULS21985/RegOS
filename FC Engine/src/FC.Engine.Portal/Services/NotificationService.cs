using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Portal.Services;

/// <summary>
/// Manages persistent in-app notifications for the FI Portal.
/// Handles notification CRUD, unread counts, and provides factory methods
/// for creating typed notifications from domain events.
/// </summary>
public class NotificationService
{
    private readonly IPortalNotificationRepository _notificationRepo;
    private readonly IInstitutionUserRepository _userRepo;
    private readonly IInstitutionRepository _institutionRepo;

    public NotificationService(
        IPortalNotificationRepository notificationRepo,
        IInstitutionUserRepository userRepo,
        IInstitutionRepository institutionRepo)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _institutionRepo = institutionRepo;
    }

    // ═══════════════════════════════════════════════════════════════
    //  QUERY METHODS
    // ═══════════════════════════════════════════════════════════════

    public async Task<NotificationListModel> GetNotifications(
        int userId, int institutionId, int page = 1, int pageSize = 20,
        NotificationType? typeFilter = null, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var all = await _notificationRepo.GetForUser(userId, institutionId, skip, pageSize + 1, ct);

        var hasMore = all.Count > pageSize;
        var items = all.Take(pageSize).ToList();

        if (typeFilter.HasValue)
            items = items.Where(n => n.Type == typeFilter.Value).ToList();

        return new NotificationListModel
        {
            Notifications = items.Select(MapToModel).ToList(),
            CurrentPage = page,
            HasNextPage = hasMore,
            UnreadCount = await _notificationRepo.GetUnreadCount(userId, institutionId, ct)
        };
    }

    public async Task<int> GetUnreadCount(int userId, int institutionId, CancellationToken ct = default)
    {
        return await _notificationRepo.GetUnreadCount(userId, institutionId, ct);
    }

    public async Task<List<NotificationModel>> GetRecentUnread(
        int userId, int institutionId, int count = 5, CancellationToken ct = default)
    {
        var items = await _notificationRepo.GetRecentUnread(userId, institutionId, count, ct);
        return items.Select(MapToModel).ToList();
    }

    public async Task MarkAsRead(int notificationId, CancellationToken ct = default)
    {
        await _notificationRepo.MarkAsRead(notificationId, ct);
    }

    public async Task MarkAllAsRead(int userId, int institutionId, CancellationToken ct = default)
    {
        await _notificationRepo.MarkAllAsRead(userId, institutionId, ct);
    }

    public async Task ClearOldRead(int userId, int institutionId, CancellationToken ct = default)
    {
        await _notificationRepo.ClearRead(userId, institutionId, DateTime.UtcNow.AddDays(-30), ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  NOTIFICATION FACTORY METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a notification when a submission is processed (Accepted/Rejected/AcceptedWithWarnings).
    /// Sent to the user who submitted the return.
    /// </summary>
    public async Task NotifySubmissionResult(
        int submittedByUserId,
        int institutionId,
        int submissionId,
        string returnCode,
        string period,
        SubmissionStatus status,
        int errorCount,
        int warningCount,
        CancellationToken ct = default)
    {
        // Check institution settings
        var settings = await GetInstitutionSettings(institutionId, ct);
        if (!settings.EmailOnSubmissionResult) return;

        var (title, message) = status switch
        {
            SubmissionStatus.Accepted =>
                ("Return Accepted", $"Your {returnCode} return for {period} has been accepted with no issues."),
            SubmissionStatus.AcceptedWithWarnings =>
                ("Return Accepted with Warnings",
                 $"Your {returnCode} return for {period} was accepted with {warningCount} warning(s). Review the validation report for details."),
            SubmissionStatus.Rejected =>
                ("Return Rejected",
                 $"Your {returnCode} return for {period} was rejected with {errorCount} error(s). Please review and re-submit."),
            _ =>
                ("Submission Update", $"Your {returnCode} return for {period} has been updated to {status}.")
        };

        await _notificationRepo.Add(new PortalNotification
        {
            UserId = submittedByUserId,
            InstitutionId = institutionId,
            Type = NotificationType.SubmissionResult,
            Title = title,
            Message = message,
            Link = $"/submissions/{submissionId}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                returnCode, period, submissionId,
                status = status.ToString(),
                errorCount, warningCount
            }),
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Create notifications when a reporting deadline is approaching.
    /// Broadcast to all users of the institution.
    /// </summary>
    public async Task NotifyDeadlineApproaching(
        int institutionId,
        string returnCode,
        string templateName,
        DateTime deadline,
        int daysRemaining,
        CancellationToken ct = default)
    {
        var settings = await GetInstitutionSettings(institutionId, ct);
        if (!settings.EmailOnDeadlineApproaching) return;

        var urgency = daysRemaining <= 1 ? "URGENT: " : "";

        await _notificationRepo.Add(new PortalNotification
        {
            UserId = null, // Broadcast
            InstitutionId = institutionId,
            Type = NotificationType.DeadlineApproaching,
            Title = $"{urgency}Deadline Approaching \u2014 {returnCode}",
            Message = $"{templateName} is due in {daysRemaining} day(s) on {deadline:d MMM yyyy}. Submit your return before the deadline.",
            Link = "/submit",
            MetadataJson = JsonSerializer.Serialize(new
            {
                returnCode, templateName,
                deadline = deadline.ToString("o"),
                daysRemaining
            }),
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Create notifications when a submission enters PendingApproval.
    /// Sent to all Checker and Admin users of the institution.
    /// </summary>
    public async Task NotifyApprovalRequest(
        int institutionId,
        int submissionId,
        string returnCode,
        string period,
        string submittedByName,
        CancellationToken ct = default)
    {
        var users = await _userRepo.GetByInstitution(institutionId, ct);
        var checkers = users
            .Where(u => u.IsActive && (u.Role == InstitutionRole.Checker || u.Role == InstitutionRole.Admin))
            .ToList();

        if (checkers.Count == 0) return;

        var notifications = checkers.Select(checker => new PortalNotification
        {
            UserId = checker.Id,
            InstitutionId = institutionId,
            Type = NotificationType.ApprovalRequest,
            Title = "Submission Pending Your Approval",
            Message = $"{submittedByName} submitted {returnCode} for {period}. Please review and approve or reject.",
            Link = $"/submissions/{submissionId}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                returnCode, period, submissionId,
                submittedBy = submittedByName
            }),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _notificationRepo.AddRange(notifications, ct);
    }

    /// <summary>
    /// Create a notification when a Checker approves or rejects a submission.
    /// Sent to the Maker who submitted the return.
    /// </summary>
    public async Task NotifyApprovalResult(
        int makerUserId,
        int institutionId,
        int submissionId,
        string returnCode,
        string period,
        bool approved,
        string reviewerName,
        string? reviewerComments,
        CancellationToken ct = default)
    {
        var title = approved ? "Submission Approved" : "Submission Rejected";

        var message = approved
            ? $"Your {returnCode} return for {period} was approved by {reviewerName}."
            : $"Your {returnCode} return for {period} was rejected by {reviewerName}." +
              (string.IsNullOrEmpty(reviewerComments) ? "" : $" Reason: \"{reviewerComments}\"");

        await _notificationRepo.Add(new PortalNotification
        {
            UserId = makerUserId,
            InstitutionId = institutionId,
            Type = NotificationType.ApprovalResult,
            Title = title,
            Message = message,
            Link = $"/submissions/{submissionId}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                returnCode, period, submissionId,
                approved, reviewerName, reviewerComments
            }),
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Create a system announcement visible to all users of an institution.
    /// </summary>
    public async Task CreateSystemAnnouncement(
        int institutionId, string title, string message, string? link = null,
        CancellationToken ct = default)
    {
        await _notificationRepo.Add(new PortalNotification
        {
            UserId = null, // Broadcast
            InstitutionId = institutionId,
            Type = NotificationType.SystemAnnouncement,
            Title = title,
            Message = message,
            Link = link,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }

    /// <summary>
    /// Create a notification when a team member change occurs.
    /// Sent to institution Admins (excluding the actor).
    /// </summary>
    public async Task NotifyTeamUpdate(
        int institutionId, string title, string message,
        int? excludeUserId = null, CancellationToken ct = default)
    {
        var users = await _userRepo.GetByInstitution(institutionId, ct);
        var admins = users
            .Where(u => u.IsActive && u.Role == InstitutionRole.Admin && u.Id != (excludeUserId ?? -1))
            .ToList();

        if (admins.Count == 0) return;

        var notifications = admins.Select(admin => new PortalNotification
        {
            UserId = admin.Id,
            InstitutionId = institutionId,
            Type = NotificationType.TeamUpdate,
            Title = title,
            Message = message,
            Link = "/institution/team",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _notificationRepo.AddRange(notifications, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task<InstitutionPortalSettings> GetInstitutionSettings(
        int institutionId, CancellationToken ct)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null || string.IsNullOrEmpty(inst.SettingsJson))
            return new InstitutionPortalSettings();

        try
        {
            return JsonSerializer.Deserialize<InstitutionPortalSettings>(inst.SettingsJson)
                   ?? new InstitutionPortalSettings();
        }
        catch { return new InstitutionPortalSettings(); }
    }

    private static NotificationModel MapToModel(PortalNotification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = n.Title,
        Message = n.Message,
        Link = n.Link,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt,
        TimeAgo = FormatTimeAgo(n.CreatedAt)
    };

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 30) return $"{(int)(diff.TotalDays / 7)}w ago";
        return utcTime.ToString("d MMM yyyy");
    }
}

// ── View Models ──────────────────────────────────────────────────────

public class NotificationModel
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string TimeAgo { get; set; } = "";
}

public class NotificationListModel
{
    public List<NotificationModel> Notifications { get; set; } = new();
    public int CurrentPage { get; set; }
    public bool HasNextPage { get; set; }
    public int UnreadCount { get; set; }
}
