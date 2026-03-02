namespace FC.Engine.Domain.ValueObjects;

public record ReportingPeriod
{
    public int Year { get; }
    public int Month { get; }
    public DateOnly ReportingDate { get; }

    public ReportingPeriod(DateOnly reportingDate)
    {
        ReportingDate = reportingDate;
        Year = reportingDate.Year;
        Month = reportingDate.Month;
    }

    public ReportingPeriod(int year, int month)
    {
        Year = year;
        Month = month;
        ReportingDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
    }

    public string ToKey() => $"{Year:D4}-{Month:D2}";

    public override string ToString() => ReportingDate.ToString("yyyy-MM-dd");
}
