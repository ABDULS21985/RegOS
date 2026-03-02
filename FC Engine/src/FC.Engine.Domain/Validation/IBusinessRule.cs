using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Returns;

namespace FC.Engine.Domain.Validation;

public interface IBusinessRule : IValidationRule
{
    IEnumerable<ValidationError> Validate(IReturnData data, BusinessRuleContext context);
}
