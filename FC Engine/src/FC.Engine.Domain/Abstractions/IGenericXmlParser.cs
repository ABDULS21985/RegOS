using FC.Engine.Domain.DataRecord;

namespace FC.Engine.Domain.Abstractions;

public interface IGenericXmlParser
{
    Task<ReturnDataRecord> Parse(Stream xmlStream, string returnCode, CancellationToken ct = default);
}
