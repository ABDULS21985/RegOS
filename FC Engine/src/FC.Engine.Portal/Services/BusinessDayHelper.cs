namespace FC.Engine.Portal.Services;

/// <summary>
/// Utilities for Nigerian business day computation with pre-loaded public holiday list.
/// </summary>
public static class BusinessDayHelper
{
    // ── Nigerian Public Holidays 2024–2030 ───────────────────────────────────
    // Sources: Nigeria Public Holidays Act + CBN official calendar
    private static readonly HashSet<DateTime> _holidays = new(
    [
        // 2024
        new(2024,  1,  1), // New Year's Day
        new(2024,  3, 29), // Good Friday
        new(2024,  4,  1), // Easter Monday
        new(2024,  4, 10), // Eid-el-Fitr
        new(2024,  4, 11), // Eid-el-Fitr (2nd day)
        new(2024,  5,  1), // Workers' Day
        new(2024,  6, 12), // Democracy Day
        new(2024,  6, 17), // Eid-el-Kabir
        new(2024,  6, 18), // Eid-el-Kabir (2nd day)
        new(2024,  9, 16), // Eid-el-Mawlid
        new(2024, 10,  1), // Independence Day
        new(2024, 12, 25), // Christmas Day
        new(2024, 12, 26), // Boxing Day

        // 2025
        new(2025,  1,  1), // New Year's Day
        new(2025,  3, 31), // Eid-el-Fitr (approx.)
        new(2025,  4,  1), // Eid-el-Fitr (2nd day)
        new(2025,  4, 18), // Good Friday
        new(2025,  4, 21), // Easter Monday
        new(2025,  5,  1), // Workers' Day
        new(2025,  6,  6), // Eid-el-Kabir (approx.)
        new(2025,  6,  7), // Eid-el-Kabir (2nd day)
        new(2025,  6, 12), // Democracy Day
        new(2025,  9,  5), // Eid-el-Mawlid (approx.)
        new(2025, 10,  1), // Independence Day
        new(2025, 12, 25), // Christmas Day
        new(2025, 12, 26), // Boxing Day

        // 2026
        new(2026,  1,  1), // New Year's Day
        new(2026,  3, 20), // Eid-el-Fitr (approx.)
        new(2026,  3, 21), // Eid-el-Fitr (2nd day)
        new(2026,  4,  3), // Good Friday
        new(2026,  4,  6), // Easter Monday
        new(2026,  5,  1), // Workers' Day
        new(2026,  5, 27), // Eid-el-Kabir (approx.)
        new(2026,  5, 28), // Eid-el-Kabir (2nd day)
        new(2026,  6, 12), // Democracy Day
        new(2026,  8, 26), // Eid-el-Mawlid (approx.)
        new(2026, 10,  1), // Independence Day
        new(2026, 12, 25), // Christmas Day
        new(2026, 12, 26), // Boxing Day

        // 2027
        new(2027,  1,  1), // New Year's Day
        new(2027,  3, 10), // Eid-el-Fitr (approx.)
        new(2027,  3, 26), // Good Friday
        new(2027,  3, 29), // Easter Monday
        new(2027,  5,  1), // Workers' Day
        new(2027,  5, 17), // Eid-el-Kabir (approx.)
        new(2027,  6, 12), // Democracy Day
        new(2027,  8, 15), // Eid-el-Mawlid (approx.)
        new(2027, 10,  1), // Independence Day
        new(2027, 12, 25), // Christmas Day
        new(2027, 12, 26), // Boxing Day

        // 2028
        new(2028,  1,  1), // New Year's Day
        new(2028,  2, 28), // Eid-el-Fitr (approx.)
        new(2028,  4, 14), // Good Friday
        new(2028,  4, 17), // Easter Monday
        new(2028,  5,  1), // Workers' Day
        new(2028,  5,  5), // Eid-el-Kabir (approx.)
        new(2028,  6, 12), // Democracy Day
        new(2028,  8,  4), // Eid-el-Mawlid (approx.)
        new(2028, 10,  1), // Independence Day
        new(2028, 12, 25), // Christmas Day
        new(2028, 12, 26), // Boxing Day

        // 2029
        new(2029,  1,  1), // New Year's Day
        new(2029,  2, 17), // Eid-el-Fitr (approx.)
        new(2029,  3, 30), // Good Friday
        new(2029,  4,  2), // Easter Monday
        new(2029,  4, 24), // Eid-el-Kabir (approx.)
        new(2029,  5,  1), // Workers' Day
        new(2029,  6, 12), // Democracy Day
        new(2029,  7, 24), // Eid-el-Mawlid (approx.)
        new(2029, 10,  1), // Independence Day
        new(2029, 12, 25), // Christmas Day
        new(2029, 12, 26), // Boxing Day

        // 2030
        new(2030,  1,  1), // New Year's Day
        new(2030,  2,  6), // Eid-el-Fitr (approx.)
        new(2030,  4, 19), // Good Friday
        new(2030,  4, 22), // Easter Monday
        new(2030,  4, 14), // Eid-el-Kabir (approx.)
        new(2030,  5,  1), // Workers' Day
        new(2030,  6, 12), // Democracy Day
        new(2030,  7, 13), // Eid-el-Mawlid (approx.)
        new(2030, 10,  1), // Independence Day
        new(2030, 12, 25), // Christmas Day
        new(2030, 12, 26), // Boxing Day
    ]);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>True if the date is a weekday (Mon-Fri) and not a Nigerian public holiday.</summary>
    public static bool IsBusinessDay(DateTime date)
        => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
           && !_holidays.Contains(date.Date);

    /// <summary>
    /// Returns the actual date of the Nth business day in the given month/year.
    /// If N exceeds the number of business days in the month, the last business day is returned.
    /// </summary>
    public static DateTime NthBusinessDay(int year, int month, int n)
    {
        var date = new DateTime(year, month, 1);
        var endOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        int count = 0;
        DateTime last = date;

        while (date <= endOfMonth)
        {
            if (IsBusinessDay(date))
            {
                last = date;
                count++;
                if (count == n) return date;
            }
            date = date.AddDays(1);
        }
        return last; // return last BD if N > total BDs in month
    }

    /// <summary>Adds N business days to a starting date.</summary>
    public static DateTime AddBusinessDays(DateTime start, int n)
    {
        var date = start;
        int added = 0;
        while (added < n)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date)) added++;
        }
        return date;
    }

    /// <summary>Returns the last business day of the given month.</summary>
    public static DateTime LastBusinessDayOfMonth(int year, int month)
    {
        var date = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        while (!IsBusinessDay(date))
            date = date.AddDays(-1);
        return date;
    }

    /// <summary>
    /// Describes a deadline date relative to its month in human-readable form.
    /// E.g. "5th BD of month", "Month-end", "15th"
    /// </summary>
    public static string DescribeDeadline(DateTime dueDate)
    {
        var lastDay = DateTime.DaysInMonth(dueDate.Year, dueDate.Month);
        if (dueDate.Day == lastDay) return "Month-end";

        // Count which business day number this is
        var start = new DateTime(dueDate.Year, dueDate.Month, 1);
        int bdCount = 0;
        for (var d = start; d <= dueDate; d = d.AddDays(1))
        {
            if (IsBusinessDay(d)) bdCount++;
        }

        if (IsBusinessDay(dueDate) && bdCount > 0)
        {
            return bdCount switch
            {
                1 => "1st BD",
                2 => "2nd BD",
                3 => "3rd BD",
                _ => $"{bdCount}th BD"
            };
        }

        var suffix = dueDate.Day switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return $"{dueDate.Day}{suffix}";
    }
}
