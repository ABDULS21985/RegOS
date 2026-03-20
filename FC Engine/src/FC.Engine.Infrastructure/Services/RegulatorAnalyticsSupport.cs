using System.Globalization;
using System.Text.Json;
using FC.Engine.Domain.Entities;

namespace FC.Engine.Infrastructure.Services;

internal static class RegulatorAnalyticsSupport
{
    internal sealed record PeriodFilter(int Year, int? Quarter, int? Month);

    public static bool TryParsePeriodCode(string? periodCode, out PeriodFilter? filter)
    {
        filter = null;
        if (string.IsNullOrWhiteSpace(periodCode))
        {
            return false;
        }

        var code = periodCode.Trim().ToUpperInvariant();
        var parts = code.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (parts[1].StartsWith("Q", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(parts[1][1..], NumberStyles.None, CultureInfo.InvariantCulture, out var quarter) &&
            quarter is >= 1 and <= 4)
        {
            filter = new PeriodFilter(year, quarter, null);
            return true;
        }

        if (int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
            month is >= 1 and <= 12)
        {
            filter = new PeriodFilter(year, null, month);
            return true;
        }

        return false;
    }

    public static string FormatPeriodCode(ReturnPeriod period)
    {
        if (period.Quarter is >= 1 and <= 4)
        {
            return $"{period.Year}-Q{period.Quarter}";
        }

        return $"{period.Year}-{period.Month:00}";
    }

    public static string FormatPeriodLabel(ReturnPeriod period)
    {
        if (period.Quarter is >= 1 and <= 4)
        {
            return $"Q{period.Quarter} {period.Year}";
        }

        var month = period.Month is < 1 or > 12 ? 1 : period.Month;
        return new DateTime(period.Year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    public static int ResolveQuarter(int month, int? quarter)
    {
        if (quarter is >= 1 and <= 4)
        {
            return quarter.Value;
        }

        var safeMonth = month is < 1 or > 12 ? 1 : month;
        return ((safeMonth - 1) / 3) + 1;
    }

    public static decimal? ExtractFirstMetric(string? json, IEnumerable<string> candidateKeys)
    {
        var values = ExtractMetricValues(json, candidateKeys);
        if (values.Count == 0)
        {
            return null;
        }

        return values[0];
    }

    public static decimal ExtractSumMetric(string? json, IEnumerable<string> candidateKeys)
    {
        var values = ExtractMetricValues(json, candidateKeys);
        return values.Sum();
    }

    public static List<decimal> ExtractMetricValues(string? json, IEnumerable<string> candidateKeys)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<decimal>();
        }

        var normalizedCandidates = candidateKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeKey)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedCandidates.Count == 0)
        {
            return new List<decimal>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var results = new List<decimal>();
            ExtractRecursive(document.RootElement, normalizedCandidates, results);
            return results;
        }
        catch (JsonException)
        {
            return new List<decimal>();
        }
    }

    public static decimal Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2m;
        }

        return sorted[mid];
    }

    public static decimal Percentile(IReadOnlyList<decimal> values, decimal percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(x => x).ToList();
        var rank = (percentile / 100m) * (sorted.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
        {
            return sorted[lowerIndex];
        }

        var weight = rank - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * weight;
    }

    private static void ExtractRecursive(JsonElement element, IReadOnlyList<string> candidates, List<decimal> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = NormalizeKey(property.Name);
                    if (IsCandidateKey(key, candidates) && TryReadDecimal(property.Value, out var value))
                    {
                        values.Add(value);
                    }

                    ExtractRecursive(property.Value, candidates, values);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ExtractRecursive(item, candidates, values);
                }

                break;

            default:
                break;
        }
    }

    private static bool IsCandidateKey(string key, IReadOnlyList<string> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (key == candidate || key.Contains(candidate, StringComparison.Ordinal) || candidate.Contains(key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadDecimal(JsonElement value, out decimal number)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.TryGetDecimal(out number);

            case JsonValueKind.String:
                return decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number);

            default:
                number = 0;
                return false;
        }
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var buffer = new char[key.Length];
        var index = 0;
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[index++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer, 0, index);
    }
}
