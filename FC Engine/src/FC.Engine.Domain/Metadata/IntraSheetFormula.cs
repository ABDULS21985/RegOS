using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Metadata;

public class IntraSheetFormula
{
    public int Id { get; set; }
    public int TemplateVersionId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public FormulaType FormulaType { get; set; }
    public string TargetFieldName { get; set; } = string.Empty;
    public string? TargetLineCode { get; set; }
    public string OperandFields { get; set; } = "[]";       // JSON array of field names
    public string? OperandLineCodes { get; set; }             // JSON array of line codes
    public string? CustomExpression { get; set; }
    public decimal ToleranceAmount { get; set; }
    public decimal? TolerancePercent { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    public string? ErrorMessage { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public IntraSheetFormula Clone()
    {
        return new IntraSheetFormula
        {
            RuleCode = RuleCode,
            RuleName = RuleName,
            FormulaType = FormulaType,
            TargetFieldName = TargetFieldName,
            TargetLineCode = TargetLineCode,
            OperandFields = OperandFields,
            OperandLineCodes = OperandLineCodes,
            CustomExpression = CustomExpression,
            ToleranceAmount = ToleranceAmount,
            TolerancePercent = TolerancePercent,
            Severity = Severity,
            ErrorMessage = ErrorMessage,
            IsActive = IsActive,
            SortOrder = SortOrder,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = CreatedBy
        };
    }
}
