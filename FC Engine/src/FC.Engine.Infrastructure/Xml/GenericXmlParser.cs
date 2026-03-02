using System.Xml.Linq;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Xml;

public class GenericXmlParser : IGenericXmlParser
{
    private readonly ITemplateMetadataCache _cache;

    public GenericXmlParser(ITemplateMetadataCache cache) => _cache = cache;

    public async Task<ReturnDataRecord> Parse(Stream xmlStream, string returnCode, CancellationToken ct = default)
    {
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        var version = template.CurrentVersion;
        var fields = version.Fields;

        var doc = XDocument.Load(xmlStream);
        XNamespace ns = template.XmlNamespace;
        var root = doc.Root!;

        var category = Enum.Parse<StructuralCategory>(template.StructuralCategory);
        var record = new ReturnDataRecord(returnCode, version.Id, category);

        switch (category)
        {
            case StructuralCategory.FixedRow:
                ParseFixedRow(root, ns, fields, record);
                break;
            case StructuralCategory.MultiRow:
                ParseMultiRow(root, ns, fields, record);
                break;
            case StructuralCategory.ItemCoded:
                ParseItemCoded(root, ns, fields, record, version.ItemCodes);
                break;
        }

        return record;
    }

    private static void ParseFixedRow(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record)
    {
        var row = new ReturnDataRow();

        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;

        foreach (var field in fields)
        {
            var element = dataSection.Element(ns + field.XmlElementName)
                       ?? dataSection.Descendants(ns + field.XmlElementName).FirstOrDefault();

            if (element != null && !string.IsNullOrWhiteSpace(element.Value))
            {
                row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
            }
        }

        record.AddRow(row);
    }

    private static void ParseMultiRow(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record)
    {
        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;

        var rowElements = dataSection.Elements().Where(e => e.Name.LocalName != "Header");
        int serialNo = 1;

        foreach (var rowElement in rowElements)
        {
            var row = new ReturnDataRow { RowKey = serialNo.ToString() };

            foreach (var field in fields)
            {
                if (field.FieldName == "serial_no")
                {
                    row.SetValue("serial_no", serialNo);
                    continue;
                }

                var element = rowElement.Element(ns + field.XmlElementName);
                if (element != null && !string.IsNullOrWhiteSpace(element.Value))
                {
                    row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
                }
            }

            record.AddRow(row);
            serialNo++;
        }
    }

    private static void ParseItemCoded(XElement root, XNamespace ns,
        IReadOnlyList<TemplateField> fields, ReturnDataRecord record,
        IReadOnlyList<TemplateItemCode> expectedItemCodes)
    {
        var dataSection = root.Elements()
            .FirstOrDefault(e => e.Name.LocalName != "Header") ?? root;

        var rowElements = dataSection.Elements().Where(e => e.Name.LocalName != "Header");

        foreach (var rowElement in rowElements)
        {
            var itemCodeField = fields.FirstOrDefault(f => f.IsKeyField);
            var itemCodeElement = rowElement.Element(ns + (itemCodeField?.XmlElementName ?? "ItemCode"));
            var itemCodeValue = itemCodeElement?.Value;

            if (string.IsNullOrWhiteSpace(itemCodeValue)) continue;

            var row = new ReturnDataRow { RowKey = itemCodeValue };
            row.SetValue(itemCodeField?.FieldName ?? "item_code", itemCodeValue);

            foreach (var field in fields.Where(f => !f.IsKeyField))
            {
                var element = rowElement.Element(ns + field.XmlElementName);
                if (element != null && !string.IsNullOrWhiteSpace(element.Value))
                {
                    row.SetValue(field.FieldName, ConvertValue(element.Value, field.DataType));
                }
            }

            record.AddRow(row);
        }
    }

    private static object? ConvertValue(string text, FieldDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        return dataType switch
        {
            FieldDataType.Money or FieldDataType.Decimal or FieldDataType.Percentage
                => decimal.TryParse(text, out var d) ? d : null,
            FieldDataType.Integer
                => int.TryParse(text, out var i) ? (object)i : null,
            FieldDataType.Date
                => DateTime.TryParse(text, out var dt) ? dt : null,
            FieldDataType.Boolean
                => bool.TryParse(text, out var b) ? b : null,
            FieldDataType.Text => text,
            _ => text
        };
    }
}
