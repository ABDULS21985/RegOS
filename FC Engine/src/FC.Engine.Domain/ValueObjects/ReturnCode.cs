using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.ValueObjects;

public record ReturnCode
{
    public string Value { get; }
    public string Prefix { get; }
    public int Number { get; }
    public string? Suffix { get; }
    public ReturnFrequency Frequency { get; }

    private ReturnCode(string value, string prefix, int number, string? suffix, ReturnFrequency frequency)
    {
        Value = value;
        Prefix = prefix;
        Number = number;
        Suffix = suffix;
        Frequency = frequency;
    }

    public static ReturnCode Parse(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();

        // Handle formats: "MFCR 300", "QFCR 364", "SFCR 1900", "FC CAR 1", "FC ACR", "FC RATING"
        string prefix;
        string remainder;

        if (normalized.StartsWith("MFCR"))
        {
            prefix = "MFCR";
            remainder = normalized[4..].Trim();
        }
        else if (normalized.StartsWith("QFCR"))
        {
            prefix = "QFCR";
            remainder = normalized[4..].Trim();
        }
        else if (normalized.StartsWith("SFCR"))
        {
            prefix = "SFCR";
            remainder = normalized[4..].Trim();
        }
        else if (normalized.StartsWith("FC"))
        {
            prefix = "FC";
            remainder = normalized[2..].Trim();
        }
        else
        {
            throw new ArgumentException($"Unknown return code prefix in '{code}'");
        }

        // Parse number and optional suffix
        // Examples: "300", "306-1", "CAR 1", "ACR", "RATING"
        int number = 0;
        string? suffix = null;

        if (remainder.Contains('-'))
        {
            var parts = remainder.Split('-', 2);
            number = int.Parse(parts[0]);
            suffix = parts[1];
        }
        else if (prefix == "FC")
        {
            // FC returns: "CAR 1", "CAR 2", "ACR", "FHR", "CVR", "RATING"
            var fcParts = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fcParts.Length == 2 && int.TryParse(fcParts[1], out var fcSuffix))
            {
                // "CAR 1" -> number=0, suffix="CAR_1"
                number = 0;
                suffix = $"{fcParts[0]}_{fcSuffix}";
            }
            else
            {
                number = 0;
                suffix = remainder.Replace(' ', '_');
            }
        }
        else
        {
            // Handle "351(2)" format
            if (remainder.Contains('('))
            {
                var parenIdx = remainder.IndexOf('(');
                number = int.Parse(remainder[..parenIdx]);
                suffix = remainder[parenIdx..].Trim('(', ')');
            }
            else
            {
                number = int.Parse(remainder);
            }
        }

        var frequency = prefix switch
        {
            "MFCR" => ReturnFrequency.Monthly,
            "QFCR" => ReturnFrequency.Quarterly,
            "SFCR" => ReturnFrequency.SemiAnnual,
            "FC" => ReturnFrequency.Computed,
            _ => ReturnFrequency.Monthly
        };

        return new ReturnCode(normalized, prefix, number, suffix, frequency);
    }

    public string ToTableName()
    {
        if (Prefix == "FC")
        {
            var name = Suffix?.ToLowerInvariant() ?? "unknown";
            return $"fc_{name}";
        }

        var tableName = $"{Prefix.ToLowerInvariant()}_{Number}";
        if (Suffix != null)
            tableName += $"_{Suffix.ToLowerInvariant().Replace('(', '_').Replace(')', '_').TrimEnd('_')}";

        return tableName;
    }

    public string ToXsdFileName()
    {
        var name = Value.Replace(" ", "").Replace("-", "_").Replace("(", "_").Replace(")", "");
        return $"{name}.xsd";
    }

    public override string ToString() => Value;
}
