using FC.Engine.Domain.Returns;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Validation;

public class CrossSheetValidationContext
{
    private readonly Dictionary<string, IReturnData> _returns = new();

    public void Register(ReturnCode code, IReturnData data)
    {
        _returns[code.Value] = data;
    }

    public T? GetReturn<T>(ReturnCode code) where T : class, IReturnData
    {
        return _returns.GetValueOrDefault(code.Value) as T;
    }

    public bool HasReturn(ReturnCode code)
    {
        return _returns.ContainsKey(code.Value);
    }

    public IReadOnlyDictionary<string, IReturnData> AllReturns => _returns;
}
