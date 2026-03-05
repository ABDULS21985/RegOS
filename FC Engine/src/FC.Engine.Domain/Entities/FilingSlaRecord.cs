namespace FC.Engine.Domain.Entities;

/// <summary>
/// Tracks filing timeliness per tenant × module × period.
/// Created/updated when a submission is made for a period.
/// </summary>
public class FilingSlaRecord
{
    public int Id { get; set; }

    /// <summary>FK to Tenant for RLS.</summary>
    public Guid TenantId { get; set; }

    public int ModuleId { get; set; }
    public int PeriodId { get; set; }
    public int? SubmissionId { get; set; }

    public DateTime PeriodEndDate { get; set; }
    public DateTime DeadlineDate { get; set; }
    public DateTime? SubmittedDate { get; set; }

    /// <summary>Positive = early, negative = late.</summary>
    public int? DaysToDeadline { get; set; }

    public bool? OnTime { get; set; }

    // Navigation
    public Module? Module { get; set; }
    public ReturnPeriod? Period { get; set; }
    public Submission? Submission { get; set; }
}
