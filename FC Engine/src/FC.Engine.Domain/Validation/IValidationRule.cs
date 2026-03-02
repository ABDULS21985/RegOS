using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Validation;

public interface IValidationRule
{
    string RuleId { get; }
    ValidationCategory Category { get; }
    ValidationSeverity DefaultSeverity { get; }
}
