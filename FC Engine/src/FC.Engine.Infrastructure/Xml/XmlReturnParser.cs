using System.Xml;
using System.Xml.Schema;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Infrastructure.Xml;

public class XmlReturnParser : IXmlParser
{
    private readonly XmlParserFactory _parserFactory;

    public XmlReturnParser(XmlParserFactory parserFactory)
    {
        _parserFactory = parserFactory;
    }

    public IReadOnlyList<ValidationError> ValidateAgainstXsd(Stream xml, XmlSchemaSet schema)
    {
        var errors = new List<ValidationError>();
        var settings = new XmlReaderSettings
        {
            Schemas = schema,
            ValidationType = ValidationType.Schema
        };

        settings.ValidationEventHandler += (_, e) =>
        {
            errors.Add(new ValidationError
            {
                RuleId = "XSD_VALIDATION",
                Field = e.Exception?.SourceUri ?? "unknown",
                Message = e.Message,
                Severity = e.Severity == XmlSeverityType.Error
                    ? ValidationSeverity.Error
                    : ValidationSeverity.Warning,
                Category = ValidationCategory.Schema
            });
        };

        using var reader = XmlReader.Create(xml, settings);
        while (reader.Read()) { }

        return errors;
    }

    public IReturnData Parse(Stream xml, ReturnCode returnCode)
    {
        var parser = _parserFactory.GetParser(returnCode);
        return parser.Parse(xml);
    }
}
