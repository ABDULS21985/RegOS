using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Infrastructure.Xml;

public class XsdSchemaProvider : IXsdSchemaProvider
{
    private readonly Dictionary<string, XmlSchemaSet> _cache = new();
    private readonly Assembly _assembly;

    public XsdSchemaProvider()
    {
        _assembly = typeof(XsdSchemaProvider).Assembly;
    }

    public XmlSchemaSet GetSchema(ReturnCode returnCode)
    {
        var key = returnCode.Value;
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var xsdFileName = returnCode.ToXsdFileName();
        var resourceName = $"FC.Engine.Infrastructure.Xml.Schemas.{xsdFileName}";

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"XSD schema not found: {resourceName}");

        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(XmlSchema.Read(stream, null)!);
        schemaSet.Compile();

        _cache[key] = schemaSet;
        return schemaSet;
    }

    public bool HasSchema(ReturnCode returnCode)
    {
        var xsdFileName = returnCode.ToXsdFileName();
        var resourceName = $"FC.Engine.Infrastructure.Xml.Schemas.{xsdFileName}";
        return _assembly.GetManifestResourceStream(resourceName) != null;
    }
}
