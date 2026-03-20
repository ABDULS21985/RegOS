using System.Text;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Importing;

internal static class FuzzyColumnMatcher
{
    public static List<ImportColumnMapping> BuildMappings(
        IReadOnlyList<SourceColumn> sourceColumns,
        IReadOnlyList<TemplateField> fields)
    {
        var mappings = new List<ImportColumnMapping>();
        var remaining = new List<TemplateField>(fields);

        foreach (var source in sourceColumns)
        {
            var normalizedHeader = Normalize(source.Header);
            if (string.IsNullOrWhiteSpace(normalizedHeader))
            {
                continue;
            }

            // Exact matches first.
            var exact = remaining.FirstOrDefault(f =>
                string.Equals(Normalize(f.FieldName), normalizedHeader, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Normalize(f.DisplayName), normalizedHeader, StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
            {
                mappings.Add(new ImportColumnMapping
                {
                    SourceIndex = source.Index,
                    SourceHeader = source.Header,
                    TargetFieldName = exact.FieldName,
                    TargetFieldLabel = exact.DisplayName,
                    Confidence = 1.0,
                    SampleValues = source.SampleValues
                });
                remaining.Remove(exact);
                continue;
            }

            var fuzzy = remaining
                .Select(field => new
                {
                    Field = field,
                    Score = Score(normalizedHeader, field)
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (fuzzy is not null && fuzzy.Score >= 0.60)
            {
                mappings.Add(new ImportColumnMapping
                {
                    SourceIndex = source.Index,
                    SourceHeader = source.Header,
                    TargetFieldName = fuzzy.Field.FieldName,
                    TargetFieldLabel = fuzzy.Field.DisplayName,
                    Confidence = fuzzy.Score,
                    SampleValues = source.SampleValues
                });
                remaining.Remove(fuzzy.Field);
            }
            else
            {
                mappings.Add(new ImportColumnMapping
                {
                    SourceIndex = source.Index,
                    SourceHeader = source.Header,
                    Confidence = 0,
                    SampleValues = source.SampleValues
                });
            }
        }

        return mappings;
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static double Score(string normalizedHeader, TemplateField field)
    {
        var label = Normalize(field.DisplayName);
        var code = Normalize(field.FieldName);

        if (normalizedHeader.Length == 0)
        {
            return 0;
        }

        var labelScore = Similarity(normalizedHeader, label);
        var codeScore = Similarity(normalizedHeader, code);

        var containsScore = 0d;
        if ((!string.IsNullOrEmpty(label) && label.Contains(normalizedHeader, StringComparison.Ordinal))
            || normalizedHeader.Contains(label, StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(code) && code.Contains(normalizedHeader, StringComparison.Ordinal))
            || normalizedHeader.Contains(code, StringComparison.Ordinal))
        {
            containsScore = 0.8;
        }

        return Math.Max(containsScore, Math.Max(labelScore, codeScore));
    }

    private static double Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0;
        }

        var distance = LevenshteinDistance(a, b);
        return 1d - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

internal sealed record SourceColumn(int Index, string Header, List<string> SampleValues);
