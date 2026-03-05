namespace FC.Engine.Domain.Entities;

public class ReturnPeriod
{
    public int Id { get; set; }

    /// <summary>FK to Tenant for RLS.</summary>
    public Guid TenantId { get; set; }

    /// <summary>FK to Module — links this period to a specific module.</summary>
    public int? ModuleId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public int? Quarter { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public DateTime ReportingDate { get; set; }
    public bool IsOpen { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Filing Calendar (RG-12) ──

    /// <summary>Computed deadline date (period end + offset days).</summary>
    public DateTime DeadlineDate { get; set; }

    /// <summary>Per-tenant override of the deadline (e.g., regulator grants extension).</summary>
    public DateTime? DeadlineOverrideDate { get; set; }

    /// <summary>Who overrode the deadline.</summary>
    public int? DeadlineOverrideBy { get; set; }

    /// <summary>Reason for the deadline override.</summary>
    public string? DeadlineOverrideReason { get; set; }

    /// <summary>FK to auto-created draft Submission at T-60.</summary>
    public int? AutoCreatedReturnId { get; set; }

    /// <summary>Upcoming, Open, DueSoon, Overdue, Completed, Closed.</summary>
    public string Status { get; set; } = "Upcoming";

    /// <summary>
    /// Notification escalation level: 0=None, 1=T-30, 2=T-14, 3=T-7, 4=T-3, 5=T-1, 6=T+0 (overdue).
    /// </summary>
    public int NotificationLevel { get; set; }

    /// <summary>The effective deadline (override or computed).</summary>
    public DateTime EffectiveDeadline => DeadlineOverrideDate ?? DeadlineDate;

    // Navigation
    public Module? Module { get; set; }
    public Submission? AutoCreatedReturn { get; set; }
}
