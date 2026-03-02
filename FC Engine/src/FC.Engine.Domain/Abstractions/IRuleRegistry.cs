using FC.Engine.Domain.Validation;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Abstractions;

public interface IRuleRegistry
{
    IReadOnlyList<IIntraSheetRule> GetIntraSheetRules(ReturnCode returnCode);
    IReadOnlyList<ICrossSheetRule> GetCrossSheetRules(ReturnCode returnCode);
    IReadOnlyList<IBusinessRule> GetBusinessRules(ReturnCode returnCode);
}
