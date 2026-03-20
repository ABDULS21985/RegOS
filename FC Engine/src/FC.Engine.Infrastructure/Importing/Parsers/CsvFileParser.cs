using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Importing.Parsers;

public class CsvFileParser : IFileParser
{
    public string SourceFormat => "CSV";

    public bool CanHandle(string fileName)
    {
        return fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ParseResult> Parse(Stream fileStream, string fileName, IReadOnlyList<TemplateField> fields, CancellationToken ct = default)
    {
        fileStream.Position = 0;
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var preview = reader.ReadLine() ?? string.Empty;
        var delimiter = DetectDelimiter(preview);
        fileStream.Position = 0;

        using var csvReader = new StreamReader(fileStream);
        using var csv = new CsvReader(csvReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = delimiter,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            return Task.FromResult(new ParseResult
            {
                Warnings = { "No CSV headers were found." }
            });
        }

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var sourceColumns = headers
            .Select((header, idx) => new SourceColumn(idx, header?.Trim() ?? string.Empty, new List<string>()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Header))
            .ToList();

        var records = new List<Dictionary<string, string?>>();
        while (csv.Read())
        {
            var record = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasData = false;

            foreach (var source in sourceColumns)
            {
                var raw = csv.GetField(source.Index);
                var value = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasData = true;
                    if (source.SampleValues.Count < 3)
                    {
                        source.SampleValues.Add(value);
                    }
                }

                record[source.Header] = value;
            }

            if (hasData)
            {
                records.Add(record);
            }
        }

        var mappings = FuzzyColumnMatcher.BuildMappings(sourceColumns, fields);
        return Task.FromResult(new ParseResult
        {
            Records = records,
            ColumnMappings = mappings,
            UnmappedColumns = mappings
                .Where(x => string.IsNullOrWhiteSpace(x.TargetFieldName) && !x.Ignored)
                .Select(x => x.SourceHeader)
                .ToList(),
            UnmappedFields = fields
                .Where(f => mappings.All(m => !string.Equals(m.TargetFieldName, f.FieldName, StringComparison.OrdinalIgnoreCase)))
                .Select(f => f.FieldName)
                .ToList()
        });
    }

    private static string DetectDelimiter(string preview)
    {
        if (preview.Count(c => c == ';') > preview.Count(c => c == ','))
        {
            return ";";
        }

        if (preview.Count(c => c == '\t') > 0)
        {
            return "\t";
        }

        return ",";
    }
}
