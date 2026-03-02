using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Returns;

public interface IReturnData
{
    ReturnCode ReturnCode { get; }
}
