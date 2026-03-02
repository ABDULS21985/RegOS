using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Xml;

public class XsdGenerator : IXsdGenerator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly ConcurrentDictionary<string, XmlSchemaSet> _xsdCache = new();

    public XsdGenerator(ITemplateMetadataCache cache) => _cache = cache;

    public async Task<XmlSchemaSet> GenerateSchema(string returnCode, CancellationToken ct = default)
    {
        if (_xsdCache.TryGetValue(returnCode.ToUpperInvariant(), out var cached))
            return cached;

        var xml = await GenerateSchemaXml(returnCode, ct);

        var schemaSet = new XmlSchemaSet();
        using var reader = new StringReader(xml);
        schemaSet.Add(XmlSchema.Read(reader, null)!);
        schemaSet.Compile();

        _xsdCache[returnCode.ToUpperInvariant()] = schemaSet;
        return schemaSet;
    }

    public async Task<string> GenerateSchemaXml(string returnCode, CancellationToken ct = default)
    {
        var template = await _cache.GetPublishedTemplate(returnCode, ct);
        var version = template.CurrentVersion;
        var fields = version.Fields;
        var category = Enum.Parse<StructuralCategory>(template.StructuralCategory);

        var xsd = new StringBuilder();
        xsd.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xsd.AppendLine($"<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"");
        xsd.AppendLine($"           targetNamespace=\"{template.XmlNamespace}\"");
        xsd.AppendLine($"           xmlns:tns=\"{template.XmlNamespace}\"");
        xsd.AppendLine("           elementFormDefault=\"qualified\">");

        // Root element
        xsd.AppendLine($"  <xs:element name=\"{template.XmlRootElement}\">");
        xsd.AppendLine("    <xs:complexType>");
        xsd.AppendLine("      <xs:sequence>");
        xsd.AppendLine("        <xs:element name=\"Header\" type=\"tns:HeaderType\"/>");

        if (category == StructuralCategory.FixedRow)
        {
            xsd.AppendLine("        <xs:element name=\"Data\" type=\"tns:DataType\"/>");
        }
        else
        {
            xsd.AppendLine("        <xs:element name=\"Rows\">");
            xsd.AppendLine("          <xs:complexType>");
            xsd.AppendLine("            <xs:sequence>");
            xsd.AppendLine("              <xs:element name=\"Row\" type=\"tns:RowType\"");
            xsd.AppendLine("                          maxOccurs=\"unbounded\" minOccurs=\"0\"/>");
            xsd.AppendLine("            </xs:sequence>");
            xsd.AppendLine("          </xs:complexType>");
            xsd.AppendLine("        </xs:element>");
        }

        xsd.AppendLine("      </xs:sequence>");
        xsd.AppendLine("    </xs:complexType>");
        xsd.AppendLine("  </xs:element>");

        // Header type
        xsd.AppendLine("  <xs:complexType name=\"HeaderType\">");
        xsd.AppendLine("    <xs:sequence>");
        xsd.AppendLine("      <xs:element name=\"InstitutionCode\" type=\"xs:string\"/>");
        xsd.AppendLine("      <xs:element name=\"ReportingDate\" type=\"xs:date\"/>");
        xsd.AppendLine($"      <xs:element name=\"ReturnCode\" type=\"xs:string\"/>");
        xsd.AppendLine("    </xs:sequence>");
        xsd.AppendLine("  </xs:complexType>");

        // Data/Row type
        var typeName = category == StructuralCategory.FixedRow ? "DataType" : "RowType";
        xsd.AppendLine($"  <xs:complexType name=\"{typeName}\">");
        xsd.AppendLine("    <xs:sequence>");

        foreach (var field in fields.OrderBy(f => f.FieldOrder))
        {
            var xsdType = MapToXsdType(field.DataType);
            var minOccurs = field.IsRequired ? "1" : "0";
            xsd.AppendLine($"      <xs:element name=\"{field.XmlElementName}\" type=\"{xsdType}\" minOccurs=\"{minOccurs}\"/>");
        }

        xsd.AppendLine("    </xs:sequence>");
        xsd.AppendLine("  </xs:complexType>");

        // Custom types
        xsd.AppendLine("  <xs:simpleType name=\"MoneyType\">");
        xsd.AppendLine("    <xs:restriction base=\"xs:decimal\">");
        xsd.AppendLine("      <xs:fractionDigits value=\"2\"/>");
        xsd.AppendLine("      <xs:totalDigits value=\"20\"/>");
        xsd.AppendLine("    </xs:restriction>");
        xsd.AppendLine("  </xs:simpleType>");

        xsd.AppendLine("</xs:schema>");

        return xsd.ToString();
    }

    public void InvalidateCache(string returnCode)
    {
        _xsdCache.TryRemove(returnCode.ToUpperInvariant(), out _);
    }

    private static string MapToXsdType(FieldDataType dataType) => dataType switch
    {
        FieldDataType.Money => "tns:MoneyType",
        FieldDataType.Decimal => "xs:decimal",
        FieldDataType.Percentage => "xs:decimal",
        FieldDataType.Integer => "xs:int",
        FieldDataType.Text => "xs:string",
        FieldDataType.Date => "xs:date",
        FieldDataType.Boolean => "xs:boolean",
        _ => "xs:string"
    };
}
