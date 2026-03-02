namespace FC.Engine.Domain.ValueObjects;

public sealed class ReturnCode : IEquatable<ReturnCode>
{
    public string Value { get; }
    public string Prefix { get; }
    public string Number { get; }

    private ReturnCode(string value, string prefix, string number)
    {
        Value = value;
        Prefix = prefix;
        Number = number;
    }

    public static ReturnCode Parse(string input)
    {
        var trimmed = input.Trim().ToUpperInvariant();
        string prefix, number;

        if (trimmed.StartsWith("MFCR"))
        {
            prefix = "MFCR";
            number = trimmed[4..].Trim();
        }
        else if (trimmed.StartsWith("QFCR"))
        {
            prefix = "QFCR";
            number = trimmed[4..].Trim();
        }
        else if (trimmed.StartsWith("SFCR"))
        {
            prefix = "SFCR";
            number = trimmed[4..].Trim();
        }
        else if (trimmed.StartsWith("FC"))
        {
            prefix = "FC";
            number = trimmed[2..].Trim();
        }
        else
        {
            prefix = "";
            number = trimmed;
        }

        return new ReturnCode($"{prefix} {number}".Trim(), prefix, number);
    }

    public string ToTableName()
    {
        return Value.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("(", "")
            .Replace(")", "");
    }

    public string ToXmlRootElement() => Value.Replace(" ", "").Replace("-", "_");

    public string ToXmlNamespace() => $"urn:cbn:dfis:fc:{Value.Replace(" ", "").ToLowerInvariant()}";

    public bool Equals(ReturnCode? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is ReturnCode rc && Equals(rc);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
}
