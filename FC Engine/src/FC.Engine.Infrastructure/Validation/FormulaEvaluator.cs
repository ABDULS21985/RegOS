using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Validation;

public class FormulaEvaluator : IFormulaEvaluator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly ExpressionParser _expressionParser = new();

    public FormulaEvaluator(ITemplateMetadataCache cache) => _cache = cache;

    public async Task<IReadOnlyList<ValidationError>> Evaluate(ReturnDataRecord record, CancellationToken ct = default)
    {
        var template = await _cache.GetPublishedTemplate(record.ReturnCode, ct);
        var formulas = template.CurrentVersion.IntraSheetFormulas;

        if (!formulas.Any())
            return Array.Empty<ValidationError>();

        var errors = new List<ValidationError>();

        foreach (var formula in formulas)
        {
            try
            {
                var formulaErrors = EvaluateFormula(formula, record, template);
                errors.AddRange(formulaErrors);
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    RuleId = formula.RuleCode,
                    Field = formula.TargetFieldName,
                    Message = $"Error evaluating formula {formula.RuleCode}: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                    Category = ValidationCategory.IntraSheet
                });
            }
        }

        return errors;
    }

    private List<ValidationError> EvaluateFormula(IntraSheetFormula formula, ReturnDataRecord record, CachedTemplate template)
    {
        var errors = new List<ValidationError>();
        var operandFields = JsonSerializer.Deserialize<List<string>>(formula.OperandFields) ?? new();

        if (record.Category == StructuralCategory.FixedRow)
        {
            var row = record.Rows.FirstOrDefault();
            if (row == null) return errors;

            var error = EvaluateFormulaOnRow(formula, row, operandFields);
            if (error != null) errors.Add(error);
        }
        else
        {
            // For MultiRow/ItemCoded, evaluate per row
            foreach (var row in record.Rows)
            {
                var error = EvaluateFormulaOnRow(formula, row, operandFields);
                if (error != null)
                {
                    error.Field = $"{formula.TargetFieldName} (row: {row.RowKey})";
                    errors.Add(error);
                }
            }
        }

        return errors;
    }

    private ValidationError? EvaluateFormulaOnRow(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        return formula.FormulaType switch
        {
            FormulaType.Sum => EvaluateSum(formula, row, operandFields),
            FormulaType.Difference => EvaluateDifference(formula, row, operandFields),
            FormulaType.Equals => EvaluateEquals(formula, row, operandFields),
            FormulaType.GreaterThan => EvaluateComparison(formula, row, operandFields, ">"),
            FormulaType.GreaterThanOrEqual => EvaluateComparison(formula, row, operandFields, ">="),
            FormulaType.LessThan => EvaluateComparison(formula, row, operandFields, "<"),
            FormulaType.LessThanOrEqual => EvaluateComparison(formula, row, operandFields, "<="),
            FormulaType.Between => EvaluateBetween(formula, row, operandFields),
            FormulaType.Ratio => EvaluateRatio(formula, row, operandFields),
            FormulaType.Custom => EvaluateCustom(formula, row),
            FormulaType.Required => EvaluateRequired(formula, row, operandFields),
            _ => null
        };
    }

    private ValidationError? EvaluateSum(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var sum = operandFields.Sum(f => row.GetDecimal(f) ?? 0m);

        if (!WithinTolerance(targetValue, sum, formula))
        {
            return CreateError(formula, $"{sum}", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should equal sum of [{string.Join(", ", operandFields)}] ({sum})");
        }
        return null;
    }

    private ValidationError? EvaluateDifference(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        if (operandFields.Count < 2) return null;

        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var first = row.GetDecimal(operandFields[0]) ?? 0m;
        var second = row.GetDecimal(operandFields[1]) ?? 0m;
        var difference = first - second;

        if (!WithinTolerance(targetValue, difference, formula))
        {
            return CreateError(formula, $"{difference}", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should equal {operandFields[0]} - {operandFields[1]} ({difference})");
        }
        return null;
    }

    private ValidationError? EvaluateEquals(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        if (operandFields.Count == 0) return null;

        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var operandValue = row.GetDecimal(operandFields[0]) ?? 0m;

        if (!WithinTolerance(targetValue, operandValue, formula))
        {
            return CreateError(formula, $"{operandValue}", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should equal {operandFields[0]} ({operandValue})");
        }
        return null;
    }

    private ValidationError? EvaluateComparison(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields, string op)
    {
        if (operandFields.Count == 0) return null;

        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var operandValue = row.GetDecimal(operandFields[0]) ?? 0m;

        var passes = op switch
        {
            ">" => targetValue > operandValue,
            ">=" => targetValue >= operandValue - formula.ToleranceAmount,
            "<" => targetValue < operandValue,
            "<=" => targetValue <= operandValue + formula.ToleranceAmount,
            _ => true
        };

        if (!passes)
        {
            return CreateError(formula, $"{op} {operandValue}", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should be {op} {operandFields[0]} ({operandValue})");
        }
        return null;
    }

    private ValidationError? EvaluateBetween(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        if (operandFields.Count < 2) return null;

        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var lowerBound = row.GetDecimal(operandFields[0]) ?? 0m;
        var upperBound = row.GetDecimal(operandFields[1]) ?? 0m;

        if (targetValue < lowerBound - formula.ToleranceAmount || targetValue > upperBound + formula.ToleranceAmount)
        {
            return CreateError(formula, $"[{lowerBound}, {upperBound}]", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should be between {lowerBound} and {upperBound}");
        }
        return null;
    }

    private ValidationError? EvaluateRatio(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        if (operandFields.Count < 2) return null;

        var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;
        var numerator = row.GetDecimal(operandFields[0]) ?? 0m;
        var denominator = row.GetDecimal(operandFields[1]) ?? 0m;

        if (denominator == 0) return null; // Skip division by zero

        var ratio = numerator / denominator;

        if (!WithinTolerance(targetValue, ratio, formula))
        {
            return CreateError(formula, $"{ratio}", $"{targetValue}",
                $"{formula.TargetFieldName} ({targetValue}) should equal {operandFields[0]}/{operandFields[1]} ({ratio:F4})");
        }
        return null;
    }

    private ValidationError? EvaluateCustom(IntraSheetFormula formula, ReturnDataRow row)
    {
        if (string.IsNullOrWhiteSpace(formula.CustomExpression)) return null;

        // Build variable map from row values
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in row.AllFields)
        {
            if (kvp.Value != null)
            {
                var dec = row.GetDecimal(kvp.Key);
                if (dec.HasValue)
                    variables[kvp.Key] = dec.Value;
            }
        }

        var result = _expressionParser.Evaluate(formula.CustomExpression, variables);

        if (!result.Passes && !WithinTolerance(result.LeftValue, result.RightValue ?? 0m, formula))
        {
            return CreateError(formula,
                result.RightValue?.ToString() ?? "N/A",
                result.LeftValue.ToString(),
                formula.ErrorMessage ?? $"Custom formula failed: {formula.CustomExpression}");
        }
        return null;
    }

    private static ValidationError? EvaluateRequired(IntraSheetFormula formula, ReturnDataRow row, List<string> operandFields)
    {
        foreach (var fieldName in operandFields)
        {
            var value = row.GetValue(fieldName);
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationError
                {
                    RuleId = formula.RuleCode,
                    Field = fieldName,
                    Message = formula.ErrorMessage ?? $"Field '{fieldName}' is required",
                    Severity = formula.Severity,
                    Category = ValidationCategory.IntraSheet
                };
            }
        }
        return null;
    }

    private static bool WithinTolerance(decimal actual, decimal expected, IntraSheetFormula formula)
    {
        var diff = Math.Abs(actual - expected);

        // Check absolute tolerance
        if (diff <= formula.ToleranceAmount)
            return true;

        // Check percentage tolerance
        if (formula.TolerancePercent.HasValue && expected != 0)
        {
            var percentDiff = (diff / Math.Abs(expected)) * 100;
            return percentDiff <= formula.TolerancePercent.Value;
        }

        return false;
    }

    private static ValidationError CreateError(IntraSheetFormula formula, string expected, string actual, string? message = null)
    {
        return new ValidationError
        {
            RuleId = formula.RuleCode,
            Field = formula.TargetFieldName,
            Message = message ?? formula.ErrorMessage ?? $"Formula {formula.RuleCode} validation failed",
            Severity = formula.Severity,
            Category = ValidationCategory.IntraSheet,
            ExpectedValue = expected,
            ActualValue = actual
        };
    }
}
