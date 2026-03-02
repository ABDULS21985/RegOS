using System.Xml.Schema;

namespace FC.Engine.Domain.Abstractions;

public interface IXsdGenerator
{
    Task<XmlSchemaSet> GenerateSchema(string returnCode, CancellationToken ct = default);
    Task<string> GenerateSchemaXml(string returnCode, CancellationToken ct = default);
    void InvalidateCache(string returnCode);
}
