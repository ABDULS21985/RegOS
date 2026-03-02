using System.Xml.Schema;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Abstractions;

public interface IXmlParser
{
    IReadOnlyList<ValidationError> ValidateAgainstXsd(Stream xml, XmlSchemaSet schema);
    IReturnData Parse(Stream xml, ReturnCode returnCode);
}
