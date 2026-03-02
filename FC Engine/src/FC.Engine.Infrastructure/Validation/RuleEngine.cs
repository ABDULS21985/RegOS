using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Returns;
using FC.Engine.Domain.Validation;

namespace FC.Engine.Infrastructure.Validation;

public class RuleEngine : IValidationEngine
{
    private readonly IRuleRegistry _ruleRegistry;
    private readonly IReturnRepository _returnRepo;

    public RuleEngine(IRuleRegistry ruleRegistry, IReturnRepository returnRepo)
    {
        _ruleRegistry = ruleRegistry;
        _returnRepo = returnRepo;
    }

    public async Task<ValidationReport> Validate(
        IReturnData data,
        Submission submission,
        CancellationToken ct = default)
    {
        var report = ValidationReport.Create(submission.Id);
        var returnCode = data.ReturnCode;

        // Phase 1: Intra-sheet formula validation
        var formulaRules = _ruleRegistry.GetIntraSheetRules(returnCode);
        foreach (var rule in formulaRules)
        {
            report.AddErrors(rule.Validate(data));
        }

        // Phase 2: Cross-sheet validation
        var crossRules = _ruleRegistry.GetCrossSheetRules(returnCode);
        if (crossRules.Count > 0)
        {
            var context = await BuildCrossSheetContext(submission, crossRules, ct);
            context.Register(returnCode, data);
            foreach (var rule in crossRules)
            {
                report.AddErrors(rule.Validate(context));
            }
        }

        // Phase 3: Business rules
        var bizRules = _ruleRegistry.GetBusinessRules(returnCode);
        if (bizRules.Count > 0 && submission.ReturnPeriod != null)
        {
            var bizContext = new BusinessRuleContext(submission.ReturnPeriod);
            foreach (var rule in bizRules)
            {
                report.AddErrors(rule.Validate(data, bizContext));
            }
        }

        report.FinalizeAt(DateTime.UtcNow);
        return report;
    }

    private async Task<CrossSheetValidationContext> BuildCrossSheetContext(
        Submission submission,
        IReadOnlyList<Domain.Validation.ICrossSheetRule> rules,
        CancellationToken ct)
    {
        var context = new CrossSheetValidationContext();
        var requiredCodes = rules
            .SelectMany(r => r.RequiredReturnCodes)
            .DistinctBy(c => c.Value);

        foreach (var code in requiredCodes)
        {
            var existingData = await _returnRepo.GetBySubmissionPeriod(
                submission.InstitutionId,
                submission.ReturnPeriodId,
                code, ct);
            if (existingData != null)
                context.Register(code, existingData);
        }

        return context;
    }
}
