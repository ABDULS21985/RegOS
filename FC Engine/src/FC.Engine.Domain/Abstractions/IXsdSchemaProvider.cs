using System.Xml.Schema;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Abstractions;

public interface IXsdSchemaProvider
{
    XmlSchemaSet GetSchema(ReturnCode returnCode);
    bool HasSchema(ReturnCode returnCode);
}
