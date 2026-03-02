using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Xml.Parsers;

namespace FC.Engine.Infrastructure.Xml;

public class XmlParserFactory
{
    private readonly Dictionary<string, IReturnXmlParser> _parsers;

    public XmlParserFactory(IEnumerable<IReturnXmlParser> parsers)
    {
        _parsers = parsers.ToDictionary(
            p => p.ReturnCodeValue.ToUpperInvariant(),
            p => p);
    }

    public IReturnXmlParser GetParser(ReturnCode returnCode)
    {
        if (_parsers.TryGetValue(returnCode.Value, out var parser))
            return parser;

        throw new NotSupportedException(
            $"No XML parser registered for return code '{returnCode.Value}'");
    }
}
