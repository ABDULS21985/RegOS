using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Validation;

public interface IIntraSheetRule : IValidationRule
{
    ReturnCode ApplicableReturnCode { get; }
    IEnumerable<ValidationError> Validate(IReturnData data);
}
