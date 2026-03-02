namespace FC.Engine.Domain.ValueObjects;

public sealed record ReportingPeriod(int Year, int Month)
{
    public DateTime ReportingDate => new DateTime(Year, Month, DateTime.DaysInMonth(Year, Month));

    public override string ToString() => $"{Year}-{Month:D2}";
}
