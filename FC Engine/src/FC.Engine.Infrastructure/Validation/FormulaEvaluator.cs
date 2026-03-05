using System.Text.Json;
using System.Text.RegularExpressions;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;

namespace FC.Engine.Infrastructure.Validation;

public partial class FormulaEvaluator : IFormulaEvaluator
{
    private readonly ITemplateMetadataCache _cache;
    private readonly ExpressionParser _expressionParser = new();
    private readonly Dictionary<string, Func<Dictionary<string, decimal>, decimal>> _customFunctions;

    public FormulaEvaluator(ITemplateMetadataCache cache)
    {
        _cache = cache;
        _customFunctions = BuildCustomFunctions();
    }

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

        var customCall = TryParseCustomFunctionCall(formula.CustomExpression);
        if (customCall != null)
        {
            var computed = EvaluateCustomFunction(customCall.Value.FunctionName, customCall.Value.Arguments, row);
            var targetValue = row.GetDecimal(formula.TargetFieldName) ?? 0m;

            var passed = customCall.Value.FunctionName.Equals("RATE_BAND_CHECK", StringComparison.OrdinalIgnoreCase)
                ? computed >= 1m
                : WithinTolerance(targetValue, computed, formula);

            if (!passed)
            {
                var expected = customCall.Value.FunctionName.Equals("RATE_BAND_CHECK", StringComparison.OrdinalIgnoreCase)
                    ? "within allowed band"
                    : computed.ToString();

                return CreateError(
                    formula,
                    expected,
                    targetValue.ToString(),
                    formula.ErrorMessage
                    ?? $"Custom function {customCall.Value.FunctionName} failed");
            }

            return null;
        }

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

    private decimal EvaluateCustomFunction(
        string functionName,
        IReadOnlyList<string> argumentTokens,
        ReturnDataRow row)
    {
        if (!_customFunctions.TryGetValue(functionName, out var evaluator))
        {
            throw new InvalidOperationException($"Unknown custom function: {functionName}");
        }

        var args = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var positional = 1;
        foreach (var token in argumentTokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var trimmed = token.Trim();
            if (trimmed.Contains('='))
            {
                var split = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
                var key = split[0];
                var valueToken = split[1];
                args[key] = ResolveTokenValue(valueToken, row);
            }
            else
            {
                var value = ResolveTokenValue(trimmed, row);
                args[$"arg{positional}"] = value;
                args[trimmed] = value;
                positional++;
            }
        }

        return evaluator(args);
    }

    private static decimal ResolveTokenValue(string token, ReturnDataRow row)
    {
        if (decimal.TryParse(token, out var numeric))
        {
            return numeric;
        }

        return row.GetDecimal(token) ?? 0m;
    }

    private static (string FunctionName, List<string> Arguments)? TryParseCustomFunctionCall(string expression)
    {
        var match = CustomFunctionRegex().Match(expression.Trim());
        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups["name"].Value.Trim();
        var argsText = match.Groups["args"].Value.Trim();
        var args = string.IsNullOrWhiteSpace(argsText)
            ? new List<string>()
            : argsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

        return (name, args);
    }

    private static Dictionary<string, Func<Dictionary<string, decimal>, decimal>> BuildCustomFunctions()
    {
        return new Dictionary<string, Func<Dictionary<string, decimal>, decimal>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CAR"] = args =>
            {
                var tier1 = Arg(args, "tier1", "arg1");
                var tier2 = Arg(args, "tier2", "arg2");
                var rwa = Arg(args, "rwa", "arg3");
                return rwa == 0 ? 0 : Math.Round((tier1 + tier2) / rwa * 100m, 2);
            },
            ["NPL_RATIO"] = args =>
            {
                var stage3 = Arg(args, "stage3", "npl_amount", "arg1");
                var totalLoans = Arg(args, "total_loans", "arg2");
                return totalLoans == 0 ? 0 : Math.Round(stage3 / totalLoans * 100m, 2);
            },
            ["LCR"] = args =>
            {
                var hqla = Arg(args, "hqla", "arg1");
                var outflow = Arg(args, "net_outflow_30d", "arg2");
                return outflow == 0 ? 0 : Math.Round(hqla / outflow * 100m, 2);
            },
            ["NSFR"] = args =>
            {
                var asf = Arg(args, "available_stable_funding", "arg1");
                var rsf = Arg(args, "required_stable_funding", "arg2");
                return rsf == 0 ? 0 : Math.Round(asf / rsf * 100m, 2);
            },
            ["ECL"] = args =>
            {
                var pd = Arg(args, "pd", "arg1");
                var lgd = Arg(args, "lgd", "arg2");
                var ead = Arg(args, "ead", "arg3");
                return Math.Round(pd * lgd * ead, 2);
            },
            ["OSS_RATIO"] = args =>
            {
                var operatingRevenue = Arg(args, "operating_revenue", "arg1");
                var totalExpenses = Arg(args, "total_expenses", "arg2");
                return totalExpenses == 0 ? 0 : Math.Round(operatingRevenue / totalExpenses * 100m, 2);
            },
            ["PAR_RATIO"] = args =>
            {
                var parAmount = Arg(args, "par_amount", "arg1");
                var grossPortfolio = Arg(args, "gross_portfolio", "arg2");
                return grossPortfolio == 0 ? 0 : Math.Round(parAmount / grossPortfolio * 100m, 2);
            },
            ["SOLVENCY_MARGIN"] = args =>
            {
                var assets = Arg(args, "admitted_assets", "arg1");
                var liabilities = Arg(args, "total_liabilities", "arg2");
                return Math.Round(assets - liabilities, 2);
            },
            ["COMBINED_RATIO"] = args =>
            {
                var claims = Arg(args, "claims_ratio", "arg1");
                var expenses = Arg(args, "expense_ratio", "arg2");
                return Math.Round(claims + expenses, 2);
            },
            ["RATE_BAND_CHECK"] = args =>
            {
                var actual = Arg(args, "actual_rate", "arg1");
                var reference = Arg(args, "reference_rate", "arg2");
                var bandPercent = Arg(args, "band_percent", "arg3", defaultValue: 10m);
                var lowerBound = reference * (1 - bandPercent / 100m);
                var upperBound = reference * (1 + bandPercent / 100m);
                return actual >= lowerBound && actual <= upperBound ? 1m : 0m;
            },
            ["NDIC_DPAS_RAW"] = args =>
            {
                var insurableDeposits = Arg(args, "insurable_deposits", "arg1");
                var assessmentRate = Arg(args, "assessment_rate", "arg2");
                return Math.Round(insurableDeposits * assessmentRate, 2);
            },
            ["NDIC_DPAS_PREMIUM"] = args =>
            {
                var insurableDeposits = Arg(args, "insurable_deposits", "arg1");
                var assessmentRate = Arg(args, "assessment_rate", "arg2");
                var minimumPremium = Arg(args, "minimum_premium", "arg3");
                var rawPremium = insurableDeposits * assessmentRate;
                return Math.Round(Math.Max(rawPremium, minimumPremium), 2);
            }
        };
    }

    private static decimal Arg(
        IReadOnlyDictionary<string, decimal> args,
        string primary,
        string? fallback1 = null,
        string? fallback2 = null,
        decimal defaultValue = 0m)
    {
        if (args.TryGetValue(primary, out var value))
        {
            return value;
        }

        if (fallback1 != null && args.TryGetValue(fallback1, out value))
        {
            return value;
        }

        if (fallback2 != null && args.TryGetValue(fallback2, out value))
        {
            return value;
        }

        return defaultValue;
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

    [GeneratedRegex(@"^FUNC:(?<name>[A-Za-z_][A-Za-z0-9_]*)\((?<args>.*)\)$", RegexOptions.Compiled)]
    private static partial Regex CustomFunctionRegex();
}
