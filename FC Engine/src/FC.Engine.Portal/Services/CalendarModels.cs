namespace FC.Engine.Portal.Services;

/// <summary>
/// Aggregated calendar data for a single institution.
/// </summary>
public class CalendarData
{
    public List<CalendarEntry> Entries { get; set; } = new();
    public CalendarSummary Summary { get; set; } = new();
}

/// <summary>
/// A single reporting obligation: one (template × period) combination.
/// </summary>
public class CalendarEntry
{
    public string ReturnCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string? ModuleCode { get; set; }
    public string? ModuleName { get; set; }
    public string Frequency { get; set; } = "";
    public DateTime DueDate { get; set; }
    public string PeriodLabel { get; set; } = "";
    public string PeriodValue { get; set; } = "";
    public CalendarEntryStatus Status { get; set; }
    public int? SubmissionId { get; set; }
    public int DaysUntilDue { get; set; }
    public string StartHref { get; set; } = "/submit";
    public string? WorkspaceHref { get; set; }
    /// <summary>Human-readable deadline description e.g. "Month-end", "5th BD", "Quarter-end".</summary>
    public string DeadlineDescription { get; set; } = "";
}

/// <summary>
/// A color-coded regulatory cycle band displayed above each calendar week row.
/// </summary>
/// <param name="BandType">weekly | monthly | quarterly | annual</param>
/// <param name="Label">Display text e.g. "CBN Weekly", "Monthly"</param>
public record RegulatoryBand(string BandType, string Label);

/// <summary>
/// Groups a week's 7 days with their associated regulatory cycle bands.
/// </summary>
public class CalendarWeekRow
{
    public List<CalendarDay> Days { get; set; } = new();
    public List<RegulatoryBand> Bands { get; set; } = new();
}

/// <summary>
/// Period summary statistics for the current month.
/// </summary>
public class CalendarSummary
{
    public int TotalDueThisMonth { get; set; }
    public int SubmittedThisMonth { get; set; }
    public int OutstandingThisMonth { get; set; }
    public int OverdueThisMonth { get; set; }
    public int CompliancePercentage { get; set; }
}

/// <summary>
/// Represents a single day in the calendar grid.
/// </summary>
public class CalendarDay
{
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public List<CalendarEntry> Entries { get; set; } = new();
}

public enum CalendarEntryStatus
{
    NotStarted,
    Draft,
    Submitted,
    Rejected,
    Overdue
}
