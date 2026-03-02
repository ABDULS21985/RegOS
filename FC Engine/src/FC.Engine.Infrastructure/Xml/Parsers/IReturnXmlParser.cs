using FC.Engine.Domain.Returns;

namespace FC.Engine.Infrastructure.Xml.Parsers;

public interface IReturnXmlParser
{
    string ReturnCodeValue { get; }
    IReturnData Parse(Stream xml);
}
