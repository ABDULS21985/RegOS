using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Metadata;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FC.Engine.Infrastructure.Importing.Parsers;

public class PdfTableFileParser : IFileParser
{
    public string SourceFormat => "PDF";

    public bool CanHandle(string fileName)
    {
        return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ParseResult> Parse(Stream fileStream, string fileName, IReadOnlyList<TemplateField> fields, CancellationToken ct = default)
    {
        fileStream.Position = 0;
        using var document = PdfDocument.Open(fileStream);

        var extractedRows = new List<List<string>>();
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords()
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var rows = GroupIntoRows(words, tolerance: 3.0);
            foreach (var row in rows)
            {
                var cells = GroupIntoCells(row, tolerance: 12.0);
                if (cells.Count > 0)
                {
                    extractedRows.Add(cells);
                }
            }
        }

        if (extractedRows.Count == 0)
        {
            return Task.FromResult(new ParseResult
            {
                Warnings = { "No tabular rows could be extracted from PDF." }
            });
        }

        var headerRowIndex = DetectHeaderRow(extractedRows, fields);
        var header = extractedRows[headerRowIndex];
        var sourceColumns = header
            .Select((h, idx) => new SourceColumn(idx, h?.Trim() ?? string.Empty, new List<string>()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Header))
            .ToList();

        var records = new List<Dictionary<string, string?>>();
        for (var i = headerRowIndex + 1; i < extractedRows.Count; i++)
        {
            var row = extractedRows[i];
            var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasData = false;

            foreach (var source in sourceColumns)
            {
                var value = source.Index < row.Count ? row[source.Index]?.Trim() : null;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasData = true;
                    if (source.SampleValues.Count < 3)
                    {
                        source.SampleValues.Add(value);
                    }
                }

                record[source.Header] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (hasData)
            {
                records.Add(record);
            }
        }

        var mappings = FuzzyColumnMatcher.BuildMappings(sourceColumns, fields);
        var parseResult = new ParseResult
        {
            Records = records,
            ColumnMappings = mappings,
            UnmappedColumns = mappings.Where(x => string.IsNullOrWhiteSpace(x.TargetFieldName) && !x.Ignored)
                .Select(x => x.SourceHeader)
                .ToList(),
            UnmappedFields = fields
                .Where(f => mappings.All(m => !string.Equals(m.TargetFieldName, f.FieldName, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.FieldName)
                .ToList()
        };

        if (records.Count == 0)
        {
            parseResult.Warnings.Add("PDF parsing completed but no data rows were detected after header mapping.");
        }

        return Task.FromResult(parseResult);
    }

    private static int DetectHeaderRow(IReadOnlyList<List<string>> rows, IReadOnlyList<TemplateField> fields)
    {
        var maxCheck = Math.Min(10, rows.Count);
        var bestRow = 0;
        var bestScore = double.MinValue;

        for (var i = 0; i < maxCheck; i++)
        {
            var normalized = rows[i]
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(FuzzyColumnMatcher.Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (normalized.Count == 0)
            {
                continue;
            }

            var score = normalized.Count(value =>
                fields.Any(f =>
                    string.Equals(FuzzyColumnMatcher.Normalize(f.FieldName), value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(FuzzyColumnMatcher.Normalize(f.DisplayName), value, StringComparison.OrdinalIgnoreCase)));

            var weighted = (double)score / normalized.Count;
            if (weighted > bestScore)
            {
                bestScore = weighted;
                bestRow = i;
            }
        }

        return bestRow;
    }

    private static List<List<Word>> GroupIntoRows(IReadOnlyList<Word> words, double tolerance)
    {
        var rows = new List<List<Word>>();

        foreach (var word in words)
        {
            var row = rows.FirstOrDefault(r => Math.Abs(r[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= tolerance);
            if (row is null)
            {
                row = new List<Word>();
                rows.Add(row);
            }

            row.Add(word);
        }

        foreach (var row in rows)
        {
            row.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
        }

        return rows
            .OrderByDescending(r => r[0].BoundingBox.Bottom)
            .ToList();
    }

    private static List<string> GroupIntoCells(IReadOnlyList<Word> row, double tolerance)
    {
        var cells = new List<string>();
        if (row.Count == 0)
        {
            return cells;
        }

        var currentWords = new List<Word> { row[0] };
        for (var i = 1; i < row.Count; i++)
        {
            var previous = row[i - 1];
            var current = row[i];
            var gap = current.BoundingBox.Left - previous.BoundingBox.Right;

            if (gap > tolerance)
            {
                cells.Add(string.Join(" ", currentWords.Select(w => w.Text)).Trim());
                currentWords.Clear();
            }

            currentWords.Add(current);
        }

        if (currentWords.Count > 0)
        {
            cells.Add(string.Join(" ", currentWords.Select(w => w.Text)).Trim());
        }

        return cells;
    }
}
