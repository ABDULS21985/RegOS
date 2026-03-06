using ClosedXML.Excel;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Metadata;
using OfficeOpenXml;

namespace FC.Engine.Infrastructure.Importing.Parsers;

public class ExcelFileParser : IFileParser
{
    public string SourceFormat => "Excel";

    public bool CanHandle(string fileName)
    {
        return fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ParseResult> Parse(Stream fileStream, string fileName, IReadOnlyList<TemplateField> fields, CancellationToken ct = default)
    {
        if (fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
            && !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ParseLegacyXls(fileStream, fields));
        }

        return Task.FromResult(ParseXlsx(fileStream, fields));
    }

    private static ParseResult ParseXlsx(Stream fileStream, IReadOnlyList<TemplateField> fields)
    {
        fileStream.Position = 0;
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheet(1);

        var headerRow = DetectHeaderRow(
            row => worksheet.Row(row).CellsUsed().Select(c => c.GetString()).ToList(),
            fields,
            maxRows: Math.Min(5, worksheet.LastRowUsed()?.RowNumber() ?? 1));

        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;

        return BuildParseResult(
            fields,
            headerRow,
            lastColumn,
            lastRow,
            col => worksheet.Cell(headerRow, col).GetString(),
            (row, col) => worksheet.Cell(row, col).GetString());
    }

    private static ParseResult ParseLegacyXls(Stream fileStream, IReadOnlyList<TemplateField> fields)
    {
        fileStream.Position = 0;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        package.Load(fileStream);

        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet is null || worksheet.Dimension is null)
        {
            return new ParseResult
            {
                Warnings = { "No worksheet data found in legacy Excel file." }
            };
        }

        var maxRowsToInspect = Math.Min(5, worksheet.Dimension.End.Row);
        var headerRow = DetectHeaderRow(
            row => Enumerable.Range(1, worksheet.Dimension.End.Column)
                .Select(col => worksheet.Cells[row, col].Text)
                .ToList(),
            fields,
            maxRowsToInspect);

        var lastColumn = worksheet.Dimension.End.Column;
        var lastRow = worksheet.Dimension.End.Row;

        return BuildParseResult(
            fields,
            headerRow,
            lastColumn,
            lastRow,
            col => worksheet.Cells[headerRow, col].Text,
            (row, col) => worksheet.Cells[row, col].Text);
    }

    private static ParseResult BuildParseResult(
        IReadOnlyList<TemplateField> fields,
        int headerRow,
        int lastColumn,
        int lastRow,
        Func<int, string> headerResolver,
        Func<int, int, string> valueResolver)
    {
        var sourceColumns = new List<SourceColumn>();
        for (var col = 1; col <= lastColumn; col++)
        {
            var header = headerResolver(col)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            var samples = new List<string>();
            for (var row = headerRow + 1; row <= lastRow && samples.Count < 3; row++)
            {
                var value = valueResolver(row, col).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    samples.Add(value);
                }
            }

            sourceColumns.Add(new SourceColumn(col, header, samples));
        }

        var mappings = FuzzyColumnMatcher.BuildMappings(sourceColumns, fields);
        var parseResult = new ParseResult
        {
            ColumnMappings = mappings,
            UnmappedColumns = mappings
                .Where(x => string.IsNullOrWhiteSpace(x.TargetFieldName) && !x.Ignored)
                .Select(x => x.SourceHeader)
                .ToList(),
            UnmappedFields = fields
                .Where(f => mappings.All(m => !string.Equals(m.TargetFieldName, f.FieldName, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.FieldName)
                .ToList()
        };

        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasData = false;
            foreach (var source in sourceColumns)
            {
                var value = valueResolver(row, source.Index)?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasData = true;
                }

                record[source.Header] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (hasData)
            {
                parseResult.Records.Add(record);
            }
        }

        return parseResult;
    }

    private static int DetectHeaderRow(
        Func<int, IReadOnlyList<string>> rowValuesProvider,
        IReadOnlyList<TemplateField> fields,
        int maxRows)
    {
        var bestRow = 1;
        var bestScore = double.MinValue;

        for (var row = 1; row <= Math.Max(1, maxRows); row++)
        {
            var rowValues = rowValuesProvider(row)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(FuzzyColumnMatcher.Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (rowValues.Count == 0)
            {
                continue;
            }

            var score = 0d;
            foreach (var value in rowValues)
            {
                if (fields.Any(f =>
                        string.Equals(FuzzyColumnMatcher.Normalize(f.FieldName), value, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(FuzzyColumnMatcher.Normalize(f.DisplayName), value, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 1;
                }
            }

            score /= rowValues.Count;
            if (score > bestScore)
            {
                bestScore = score;
                bestRow = row;
            }
        }

        return bestRow;
    }
}
