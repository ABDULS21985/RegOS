using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Infrastructure.Services;

public class TemplateDownloadService : ITemplateDownloadService
{
    private const int MaxRows = 1000;

    private readonly ITemplateMetadataCache _templateCache;

    public TemplateDownloadService(ITemplateMetadataCache templateCache)
    {
        _templateCache = templateCache;
    }

    public async Task<byte[]> GenerateTemplateExcel(Guid tenantId, string returnCode, CancellationToken ct = default)
    {
        var template = await _templateCache.GetPublishedTemplate(tenantId, returnCode, ct);
        var fields = template.CurrentVersion.Fields.OrderBy(x => x.FieldOrder).ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(template.ReturnCode));

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var colIndex = i + 1;

            var headerCell = worksheet.Cell(1, colIndex);
            headerCell.Value = field.IsRequired ? $"{field.DisplayName}*" : field.DisplayName;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#006B3F");
            headerCell.Style.Font.FontColor = XLColor.White;

            worksheet.Column(colIndex).Style.NumberFormat.Format = ResolveNumberFormat(field.DataType);

            var allowedValues = ParseAllowedValues(field.AllowedValues);
            if (allowedValues.Count > 0)
            {
                var escaped = string.Join(",", allowedValues.Select(EscapeForExcelValidation));
                worksheet.Range(2, colIndex, MaxRows, colIndex)
                    .CreateDataValidation()
                    .List(escaped);
            }

            if (!string.IsNullOrWhiteSpace(field.HelpText))
            {
                headerCell.GetComment().AddText(field.HelpText);
            }
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(1, 60);

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell("A1").Value = "Data Entry Instructions";
        instructions.Cell("A1").Style.Font.Bold = true;
        instructions.Cell("A3").Value = "1. Enter data in the first sheet only.";
        instructions.Cell("A4").Value = "2. Do not modify column headers.";
        instructions.Cell("A5").Value = "3. Required fields are marked with *.";
        instructions.Cell("A6").Value = "4. Use dropdown lists where provided.";
        instructions.Cell("A7").Value = "5. Upload the completed file via Bulk Upload.";
        instructions.Cell("A9").Value = $"Return Code: {template.ReturnCode}";
        instructions.Cell("A10").Value = $"Template: {template.Name}";
        instructions.Cell("A11").Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        instructions.Column("A").Width = 100;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<string> GenerateTemplateCsv(Guid tenantId, string returnCode, CancellationToken ct = default)
    {
        var template = await _templateCache.GetPublishedTemplate(tenantId, returnCode, ct);
        var headers = template.CurrentVersion.Fields
            .OrderBy(x => x.FieldOrder)
            .Select(x => x.IsRequired ? $"{x.DisplayName}*" : x.DisplayName)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        return sb.ToString();
    }

    private static string ResolveNumberFormat(FieldDataType dataType)
    {
        return dataType switch
        {
            FieldDataType.Money => "#,##0.00",
            FieldDataType.Percentage => "0.00%",
            FieldDataType.Decimal => "#,##0.0000",
            FieldDataType.Integer => "#,##0",
            FieldDataType.Date => "yyyy-mm-dd",
            _ => "@"
        };
    }

    private static List<string> ParseAllowedValues(string? allowedValues)
    {
        if (string.IsNullOrWhiteSpace(allowedValues))
        {
            return [];
        }

        try
        {
            var asJson = JsonSerializer.Deserialize<List<string>>(allowedValues);
            if (asJson is { Count: > 0 })
            {
                return asJson.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            }
        }
        catch
        {
            // ignored, fallback to comma-split below
        }

        return allowedValues
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static string EscapeForExcelValidation(string value)
    {
        return value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string SanitizeSheetName(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "Template";
        }

        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var clean = new string(source.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return clean.Length <= 31 ? clean : clean[..31];
    }
}
