using FC.Engine.Domain.Entities;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Validation;

public interface ICrossSheetRule : IValidationRule
{
    IReadOnlyList<ReturnCode> RequiredReturnCodes { get; }
    IEnumerable<ValidationError> Validate(CrossSheetValidationContext context);
}
