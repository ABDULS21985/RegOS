using FC.Engine.Domain.Metadata;

namespace FC.Engine.Domain.Abstractions;

public interface IFileParser
{
    string SourceFormat { get; }
    bool CanHandle(string fileName);
    Task<ParseResult> Parse(Stream fileStream, string fileName, IReadOnlyList<TemplateField> fields, CancellationToken ct = default);
}

public class ParseResult
{
    public List<Dictionary<string, string?>> Records { get; set; } = [];
    public List<ImportColumnMapping> ColumnMappings { get; set; } = [];
    public List<string> UnmappedColumns { get; set; } = [];
    public List<string> UnmappedFields { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public int RecordCount => Records.Count;
}

public class ImportColumnMapping
{
    public int SourceIndex { get; set; }
    public string SourceHeader { get; set; } = string.Empty;
    public string? TargetFieldName { get; set; }
    public string? TargetFieldLabel { get; set; }
    public double Confidence { get; set; }
    public bool Ignored { get; set; }
    public List<string> SampleValues { get; set; } = [];
}
