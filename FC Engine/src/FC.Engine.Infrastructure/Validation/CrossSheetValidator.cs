using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Validation;

public class CrossSheetValidator : ICrossSheetValidator
{
    private readonly IFormulaRepository _formulaRepo;
    private readonly IGenericDataRepository _dataRepo;
    private readonly ITemplateMetadataCache _cache;
    private readonly MetadataDbContext? _db;
    private readonly IEntitlementService? _entitlementService;
    private readonly ExpressionParser _expressionParser = new();

    public CrossSheetValidator(
        IFormulaRepository formulaRepo,
        IGenericDataRepository dataRepo,
        ITemplateMetadataCache cache,
        MetadataDbContext? db = null,
        IEntitlementService? entitlementService = null)
    {
        _formulaRepo = formulaRepo;
        _dataRepo = dataRepo;
        _cache = cache;
        _db = db;
        _entitlementService = entitlementService;
    }

    public async Task<IReadOnlyList<ValidationError>> Validate(
        ReturnDataRecord currentRecord,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default)
    {
        var rules = await _formulaRepo.GetCrossSheetRulesForTemplate(currentRecord.ReturnCode, ct);
        if (!rules.Any())
            return Array.Empty<ValidationError>();

        var errors = new List<ValidationError>();

        // Cache loaded records to avoid redundant queries
        var recordCache = new Dictionary<string, ReturnDataRecord?>(StringComparer.OrdinalIgnoreCase);
        recordCache[currentRecord.ReturnCode] = currentRecord;

        foreach (var rule in rules)
        {
            if (rule.Expression == null) continue;

            try
            {
                var error = await EvaluateRule(rule, currentRecord, institutionId, returnPeriodId, recordCache, ct);
                if (error != null) errors.Add(error);
            }
            catch (Exception ex)
            {
                errors.Add(new ValidationError
                {
                    RuleId = rule.RuleCode,
                    Field = "CrossSheet",
                    Message = $"Error evaluating cross-sheet rule {rule.RuleCode}: {ex.Message}",
                    Severity = ValidationSeverity.Error,
                    Category = ValidationCategory.CrossSheet
                });
            }
        }

        return errors;
    }

    public async Task<IReadOnlyList<ValidationError>> ValidateCrossModule(
        Guid tenantId,
        int submissionId,
        string moduleCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct = default)
    {
        if (_db is null)
        {
            return Array.Empty<ValidationError>();
        }

        var rules = await _db.CrossSheetRules
            .Include(r => r.SourceModule)
            .Include(r => r.TargetModule)
            .Where(r => r.IsActive
                        && r.SourceModuleId != null
                        && r.TargetModuleId != null
                        && r.SourceModule != null
                        && r.SourceModule.ModuleCode == moduleCode
                        && r.SourceTemplateCode != null
                        && r.SourceFieldCode != null
                        && r.TargetTemplateCode != null
                        && r.TargetFieldCode != null
                        && r.Operator != null)
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            return Array.Empty<ValidationError>();
        }

        var errors = new List<ValidationError>();
        var sourceSubmission = await _db.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (sourceSubmission is null)
        {
            return Array.Empty<ValidationError>();
        }

        foreach (var rule in rules)
        {
            if (rule.TargetModule?.ModuleCode is null)
            {
                continue;
            }

            if (_entitlementService is not null)
            {
                var hasAccess = await _entitlementService.HasModuleAccess(tenantId, rule.TargetModule.ModuleCode, ct);
                if (!hasAccess)
                {
                    continue;
                }
            }

            var sourceSubmissionId = await ResolveSourceSubmissionId(
                sourceSubmission,
                rule.SourceTemplateCode!,
                institutionId,
                returnPeriodId,
                ct);

            if (!sourceSubmissionId.HasValue)
            {
                continue;
            }

            var targetSubmission = await _db.Submissions
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId
                            && s.InstitutionId == institutionId
                            && s.ReturnPeriodId == returnPeriodId
                            && s.ReturnCode == rule.TargetTemplateCode)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync(ct);

            if (targetSubmission is null)
            {
                continue;
            }

            var sourceRecord = await _dataRepo.GetBySubmission(rule.SourceTemplateCode!, sourceSubmissionId.Value, ct);
            var targetRecord = await _dataRepo.GetBySubmission(rule.TargetTemplateCode!, targetSubmission.Id, ct);

            if (sourceRecord is null || targetRecord is null)
            {
                continue;
            }

            var sourceValue = ResolveFieldValue(sourceRecord, rule.SourceFieldCode!);
            var targetValue = ResolveFieldValue(targetRecord, rule.TargetFieldCode!);
            if (sourceValue is null || targetValue is null)
            {
                continue;
            }

            if (!EvaluateCrossModuleRule(
                    sourceValue.Value,
                    targetValue.Value,
                    rule.Operator!,
                    rule.ToleranceAmount,
                    rule.TolerancePercent))
            {
                errors.Add(new ValidationError
                {
                    RuleId = rule.RuleCode,
                    Field = rule.SourceFieldCode!,
                    Message = $"Cross-module validation failed: {rule.Description ?? rule.RuleName}. " +
                              $"Source ({rule.SourceTemplateCode}.{rule.SourceFieldCode})={sourceValue.Value}, " +
                              $"Target ({rule.TargetTemplateCode}.{rule.TargetFieldCode})={targetValue.Value}",
                    Severity = rule.Severity,
                    Category = ValidationCategory.CrossSheet,
                    ExpectedValue = $"{rule.Operator} target",
                    ActualValue = sourceValue.Value.ToString(),
                    ReferencedReturnCode = $"{rule.SourceTemplateCode},{rule.TargetTemplateCode}"
                });
            }
        }

        return errors;
    }

    private async Task<int?> ResolveSourceSubmissionId(
        Submission currentSubmission,
        string sourceTemplateCode,
        int institutionId,
        int returnPeriodId,
        CancellationToken ct)
    {
        if (_db is null)
        {
            return null;
        }

        if (string.Equals(currentSubmission.ReturnCode, sourceTemplateCode, StringComparison.OrdinalIgnoreCase))
        {
            return currentSubmission.Id;
        }

        var sourceSubmission = await _db.Submissions
            .AsNoTracking()
            .Where(s => s.TenantId == currentSubmission.TenantId
                        && s.InstitutionId == institutionId
                        && s.ReturnPeriodId == returnPeriodId
                        && s.ReturnCode == sourceTemplateCode)
            .OrderByDescending(s => s.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        return sourceSubmission?.Id;
    }

    private static decimal? ResolveFieldValue(ReturnDataRecord record, string fieldCode)
    {
        var row = record.Rows.FirstOrDefault();
        return row?.GetDecimal(fieldCode);
    }

    private static bool EvaluateCrossModuleRule(
        decimal source,
        decimal target,
        string op,
        decimal toleranceAmount,
        decimal? tolerancePercent)
    {
        var normalized = op.Trim().ToUpperInvariant();
        var adjustedTargetLower = target - toleranceAmount;
        var adjustedTargetUpper = target + toleranceAmount;

        var result = normalized switch
        {
            "EQUALS" => source >= adjustedTargetLower && source <= adjustedTargetUpper,
            "GREATERTHAN" => source > adjustedTargetLower,
            "LESSTHAN" => source < adjustedTargetUpper,
            "GREATEREQUAL" => source >= adjustedTargetLower,
            "LESSEQUAL" => source <= adjustedTargetUpper,
            _ => source >= adjustedTargetLower && source <= adjustedTargetUpper
        };

        if (result)
        {
            return true;
        }

        if (!tolerancePercent.HasValue || target == 0)
        {
            return false;
        }

        var deltaPercent = Math.Abs(source - target) / Math.Abs(target) * 100m;
        return deltaPercent <= tolerancePercent.Value;
    }

    private async Task<ValidationError?> EvaluateRule(
        Domain.Validation.CrossSheetRule rule,
        ReturnDataRecord currentRecord,
        int institutionId,
        int returnPeriodId,
        Dictionary<string, ReturnDataRecord?> recordCache,
        CancellationToken ct)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var operand in rule.Operands.OrderBy(o => o.SortOrder))
        {
            // Load the record for this operand's template
            if (!recordCache.ContainsKey(operand.TemplateReturnCode))
            {
                var record = await _dataRepo.GetByInstitutionAndPeriod(
                    operand.TemplateReturnCode, institutionId, returnPeriodId, ct);
                recordCache[operand.TemplateReturnCode] = record;
            }

            var sourceRecord = recordCache[operand.TemplateReturnCode];
            if (sourceRecord == null)
            {
                // Source record not yet submitted — skip this rule
                return null;
            }

            var value = ResolveOperandValue(operand, sourceRecord);
            variables[operand.OperandAlias] = value;
        }

        // Evaluate the expression
        if (rule.Expression is null)
            return null;

        var result = _expressionParser.Evaluate(rule.Expression.Expression, variables);

        if (!result.Passes)
        {
            // Check tolerance
            if (result.RightValue.HasValue)
            {
                var diff = Math.Abs(result.LeftValue - result.RightValue.Value);
                if (diff <= rule.Expression.ToleranceAmount)
                    return null;

                if (rule.Expression.TolerancePercent.HasValue && result.RightValue.Value != 0)
                {
                    var pctDiff = (diff / Math.Abs(result.RightValue.Value)) * 100;
                    if (pctDiff <= rule.Expression.TolerancePercent.Value)
                        return null;
                }
            }

            var operandSummary = string.Join(", ",
                rule.Operands.Select(o => $"{o.OperandAlias}={variables.GetValueOrDefault(o.OperandAlias)}"));

            return new ValidationError
            {
                RuleId = rule.RuleCode,
                Field = "CrossSheet",
                Message = rule.Expression.ErrorMessage
                    ?? $"Cross-sheet rule '{rule.RuleName}' failed: {rule.Expression.Expression} [{operandSummary}]",
                Severity = rule.Severity,
                Category = ValidationCategory.CrossSheet,
                ExpectedValue = result.RightValue?.ToString(),
                ActualValue = result.LeftValue.ToString(),
                ReferencedReturnCode = string.Join(",", rule.Operands.Select(o => o.TemplateReturnCode).Distinct())
            };
        }

        return null;
    }

    private static decimal ResolveOperandValue(Domain.Validation.CrossSheetRuleOperand operand, ReturnDataRecord record)
    {
        if (record.Category == StructuralCategory.FixedRow)
        {
            var row = record.Rows.FirstOrDefault();
            return row?.GetDecimal(operand.FieldName) ?? 0m;
        }

        // For MultiRow/ItemCoded, apply aggregate function or filter by item code
        var rows = record.Rows.AsEnumerable();

        if (!string.IsNullOrEmpty(operand.FilterItemCode))
        {
            rows = rows.Where(r => r.RowKey == operand.FilterItemCode);
        }

        var values = rows.Select(r => r.GetDecimal(operand.FieldName) ?? 0m).ToList();

        if (!values.Any()) return 0m;

        return (operand.AggregateFunction?.ToUpperInvariant()) switch
        {
            "SUM" => values.Sum(),
            "COUNT" => values.Count,
            "MAX" => values.Max(),
            "MIN" => values.Min(),
            "AVG" => values.Average(),
            null or "" => values.First(), // No aggregate: take first value
            _ => values.Sum()
        };
    }
}
