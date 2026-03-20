using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Application.Services;

public class ValidationOrchestrator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly IFormulaEvaluator _formulaEvaluator;
    private readonly ICrossSheetValidator _crossSheetValidator;
    private readonly IBusinessRuleEvaluator _businessRuleEvaluator;

    public ValidationOrchestrator(
        ITemplateMetadataCache cache,
        IFormulaEvaluator formulaEvaluator,
        ICrossSheetValidator crossSheetValidator,
        IBusinessRuleEvaluator businessRuleEvaluator)
    {
        _cache = cache;
        _formulaEvaluator = formulaEvaluator;
        _crossSheetValidator = crossSheetValidator;
        _businessRuleEvaluator = businessRuleEvaluator;
    }

    public async Task<ValidationReport> Validate(
        ReturnDataRecord record,
        Submission submission,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default)
    {
        var report = ValidationReport.Create(submission.Id, submission.TenantId);
        var template = await _cache.GetPublishedTemplate(record.ReturnCode, ct);

        // Phase 1: Type/Range validation
        var typeErrors = ValidateTypeRange(record, template);
        report.AddErrors(typeErrors);

        // Phase 2: Intra-sheet formula validation
        var formulaErrors = await _formulaEvaluator.Evaluate(record, ct);
        report.AddErrors(formulaErrors);

        // Phase 3: Cross-sheet validation (only if intra-sheet passed without errors)
        if (!report.HasErrors)
        {
            var crossSheetErrors = await _crossSheetValidator.Validate(
                record, institutionId, returnPeriodId, ct) ?? Array.Empty<ValidationError>();
            report.AddErrors(crossSheetErrors);

            if (!report.HasErrors
                && submission.TenantId != Guid.Empty
                && !string.IsNullOrWhiteSpace(template.ModuleCode))
            {
                var crossModuleErrors = await _crossSheetValidator.ValidateCrossModule(
                    submission.TenantId,
                    submission.Id,
                    template.ModuleCode,
                    institutionId,
                    returnPeriodId,
                    ct) ?? Array.Empty<ValidationError>();
                report.AddErrors(crossModuleErrors);
            }
        }

        // Phase 4: Business rules
        var businessErrors = await _businessRuleEvaluator.Evaluate(record, submission, ct);
        report.AddErrors(businessErrors);

        return report;
    }

    public async Task<ValidationReport> ValidateRelaxed(
        List<Dictionary<string, object?>> records,
        CachedTemplate template,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var report = ValidationReport.Create(0, tenantId);
        var category = Enum.Parse<StructuralCategory>(template.StructuralCategory);
        var record = new ReturnDataRecord(template.ReturnCode, template.CurrentVersion.Id, category);

        if (records.Count == 0)
        {
            record.AddRow(new ReturnDataRow());
        }
        else
        {
            foreach (var rowData in records)
            {
                var row = new ReturnDataRow();
                foreach (var pair in rowData)
                {
                    row.SetValue(pair.Key, pair.Value);
                }

                record.AddRow(row);
            }
        }

        var typeErrors = ValidateTypeRange(record, template);
        DowngradeToWarnings(typeErrors);
        report.AddErrors(typeErrors);

        var formulaErrors = await _formulaEvaluator.Evaluate(record, ct);
        DowngradeToWarnings(formulaErrors);
        report.AddErrors(formulaErrors);

        var historicalSubmission = Submission.Create(0, 0, template.ReturnCode, tenantId);
        var businessErrors = await _businessRuleEvaluator.Evaluate(record, historicalSubmission, ct);
        DowngradeToWarnings(businessErrors);
        report.AddErrors(businessErrors);

        report.FinalizeAt(DateTime.UtcNow);
        return report;
    }

    private static void DowngradeToWarnings(IEnumerable<ValidationError> errors)
    {
        foreach (var error in errors)
        {
            error.Severity = ValidationSeverity.Warning;
        }
    }

    private static List<ValidationError> ValidateTypeRange(ReturnDataRecord record, CachedTemplate template)
    {
        var errors = new List<ValidationError>();
        var fields = template.CurrentVersion.Fields;

        foreach (var row in record.Rows)
        {
            foreach (var field in fields)
            {
                var value = row.GetValue(field.FieldName);

                // Required check
                if (field.IsRequired && value == null)
                {
                    errors.Add(new ValidationError
                    {
                        RuleId = $"REQ-{field.FieldName}",
                        Field = field.FieldName,
                        Message = $"Required field '{field.DisplayName}' is missing",
                        Severity = ValidationSeverity.Error,
                        Category = ValidationCategory.TypeRange,
                        ExpectedValue = "Non-null value"
                    });
                    continue;
                }

                if (value == null) continue;

                // Range checks for numeric fields
                if (field.DataType is FieldDataType.Money or FieldDataType.Decimal or FieldDataType.Integer or FieldDataType.Percentage)
                {
                    var decVal = row.GetDecimal(field.FieldName);
                    if (decVal == null) continue;

                    if (field.MinValue != null && decimal.TryParse(field.MinValue, out var min) && decVal < min)
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"RANGE-{field.FieldName}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value {decVal} is below minimum {min}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ExpectedValue = $">= {min}",
                            ActualValue = decVal.ToString()
                        });
                    }

                    if (field.MaxValue != null && decimal.TryParse(field.MaxValue, out var max) && decVal > max)
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"RANGE-{field.FieldName}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value {decVal} exceeds maximum {max}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ExpectedValue = $"<= {max}",
                            ActualValue = decVal.ToString()
                        });
                    }
                }

                // MaxLength check for text fields
                if (field.DataType == FieldDataType.Text && field.MaxLength.HasValue)
                {
                    var strVal = value.ToString();
                    if (strVal != null && strVal.Length > field.MaxLength.Value)
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"LEN-{field.FieldName}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' exceeds max length of {field.MaxLength}",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ExpectedValue = $"<= {field.MaxLength} chars",
                            ActualValue = $"{strVal.Length} chars"
                        });
                    }
                }

                // Allowed values check
                if (field.AllowedValues != null)
                {
                    var allowed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(field.AllowedValues);
                    if (allowed != null && !allowed.Contains(value.ToString()!, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add(new ValidationError
                        {
                            RuleId = $"ENUM-{field.FieldName}",
                            Field = field.FieldName,
                            Message = $"'{field.DisplayName}' value '{value}' is not in the allowed list",
                            Severity = ValidationSeverity.Error,
                            Category = ValidationCategory.TypeRange,
                            ExpectedValue = field.AllowedValues,
                            ActualValue = value.ToString()
                        });
                    }
                }
            }
        }

        return errors;
    }
}
