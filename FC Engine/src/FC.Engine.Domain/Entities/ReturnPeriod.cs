using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class ReturnPeriod
{
    public int Id { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public ReturnFrequency Frequency { get; private set; }
    public DateOnly ReportingDate { get; private set; }
    public bool IsOpen { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private ReturnPeriod() { }

    public static ReturnPeriod Create(int year, int month, ReturnFrequency frequency)
    {
        return new ReturnPeriod
        {
            Year = year,
            Month = month,
            Frequency = frequency,
            ReportingDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            IsOpen = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Close() => IsOpen = false;
}
