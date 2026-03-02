using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Validation;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Infrastructure.Validation;

public class RuleRegistry : IRuleRegistry
{
    private readonly Dictionary<string, List<IIntraSheetRule>> _intraRules = new();
    private readonly Dictionary<string, List<ICrossSheetRule>> _crossRules = new();
    private readonly Dictionary<string, List<IBusinessRule>> _bizRules = new();

    public RuleRegistry(
        IEnumerable<IIntraSheetRule> intraSheetRules,
        IEnumerable<ICrossSheetRule> crossSheetRules,
        IEnumerable<IBusinessRule> businessRules)
    {
        foreach (var rule in intraSheetRules)
        {
            var key = rule.ApplicableReturnCode.Value;
            if (!_intraRules.ContainsKey(key))
                _intraRules[key] = new List<IIntraSheetRule>();
            _intraRules[key].Add(rule);
        }

        foreach (var rule in crossSheetRules)
        {
            foreach (var code in rule.RequiredReturnCodes)
            {
                var key = code.Value;
                if (!_crossRules.ContainsKey(key))
                    _crossRules[key] = new List<ICrossSheetRule>();
                _crossRules[key].Add(rule);
            }
        }

        foreach (var rule in businessRules)
        {
            // Business rules are registered generically; they specify applicability internally
            var key = "__all__";
            if (!_bizRules.ContainsKey(key))
                _bizRules[key] = new List<IBusinessRule>();
            _bizRules[key].Add(rule);
        }
    }

    public IReadOnlyList<IIntraSheetRule> GetIntraSheetRules(ReturnCode returnCode)
    {
        return _intraRules.TryGetValue(returnCode.Value, out var rules)
            ? rules
            : Array.Empty<IIntraSheetRule>();
    }

    public IReadOnlyList<ICrossSheetRule> GetCrossSheetRules(ReturnCode returnCode)
    {
        return _crossRules.TryGetValue(returnCode.Value, out var rules)
            ? rules
            : Array.Empty<ICrossSheetRule>();
    }

    public IReadOnlyList<IBusinessRule> GetBusinessRules(ReturnCode returnCode)
    {
        return _bizRules.TryGetValue("__all__", out var rules)
            ? rules
            : Array.Empty<IBusinessRule>();
    }
}
