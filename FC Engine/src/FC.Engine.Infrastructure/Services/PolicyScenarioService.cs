using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Models;
using FC.Engine.Infrastructure;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

public sealed class PolicyScenarioService : IPolicyScenarioService
{
    private readonly IDbContextFactory<MetadataDbContext> _dbFactory;
    private readonly IPolicyAuditLogger _audit;
    private readonly ILogger<PolicyScenarioService> _log;

    public PolicyScenarioService(
        IDbContextFactory<MetadataDbContext> dbFactory,
        IPolicyAuditLogger audit,
        ILogger<PolicyScenarioService> log)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _log = log;
    }

    // ── Create ────────────────────────────────────────────────────────

    public async Task<long> CreateScenarioAsync(
        int regulatorId,
        string title,
        string? description,
        PolicyDomain domain,
        string targetEntityTypes,
        DateOnly baselineDate,
        int createdByUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var scenario = new PolicyScenario
        {
            RegulatorId = regulatorId,
            Title = title,
            Description = description,
            PolicyDomain = domain,
            TargetEntityTypes = targetEntityTypes,
            BaselineDate = baselineDate,
            Status = PolicyStatus.Draft,
            Version = 1,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.PolicyScenarios.Add(scenario);
        await db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            scenario.Id, regulatorId, Guid.NewGuid(),
            "ScenarioCreated", new { title, domain, targetEntityTypes, baselineDate },
            createdByUserId, ct);

        _log.LogInformation(
            "Created policy scenario {ScenarioId} '{Title}' for Regulator={RegulatorId}",
            scenario.Id, title, regulatorId);

        return scenario.Id;
    }

    // ── Parameters ────────────────────────────────────────────────────

    public async Task AddParameterAsync(
        long scenarioId,
        int regulatorId,
        string parameterCode,
        decimal proposedValue,
        string? applicableEntityTypes,
        int userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var scenario = await db.PolicyScenarios
            .FirstOrDefaultAsync(s => s.Id == scenarioId && s.RegulatorId == regulatorId, ct)
            ?? throw new InvalidOperationException($"Scenario {scenarioId} not found for regulator {regulatorId}.");

        var preset = await db.PolicyParameterPresets
            .FirstOrDefaultAsync(p => p.ParameterCode == parameterCode && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Parameter preset '{parameterCode}' not found or inactive.");

        var nextOrder = await db.PolicyParameters
            .Where(p => p.ScenarioId == scenarioId)
            .CountAsync(ct) + 1;

        var param = new PolicyParameter
        {
            ScenarioId = scenarioId,
            ParameterCode = parameterCode,
            ParameterName = preset.ParameterName,
            CurrentValue = preset.CurrentBaseline,
            ProposedValue = proposedValue,
            Unit = preset.Unit,
            ApplicableEntityTypes = applicableEntityTypes ?? "ALL",
            ReturnLineReference = preset.ReturnLineReference,
            DisplayOrder = nextOrder,
            CreatedAt = DateTime.UtcNow
        };

        db.PolicyParameters.Add(param);

        if (scenario.Status == PolicyStatus.Draft)
        {
            scenario.Status = PolicyStatus.ParametersSet;
            scenario.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            scenarioId, regulatorId, Guid.NewGuid(),
            "ParameterAdded",
            new { parameterCode, proposedValue, currentValue = preset.CurrentBaseline },
            userId, ct);

        _log.LogInformation(
            "Added parameter {ParameterCode} to Scenario={ScenarioId}, proposed={ProposedValue}",
            parameterCode, scenarioId, proposedValue);
    }

    public async Task UpdateParameterAsync(
        long scenarioId,
        int regulatorId,
        string parameterCode,
        decimal newProposedValue,
        int userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Verify regulator access on the parent scenario
        var scenarioExists = await db.PolicyScenarios
            .AnyAsync(s => s.Id == scenarioId && s.RegulatorId == regulatorId, ct);

        if (!scenarioExists)
            throw new InvalidOperationException($"Scenario {scenarioId} not found for regulator {regulatorId}.");

        var param = await db.PolicyParameters
            .FirstOrDefaultAsync(p => p.ScenarioId == scenarioId && p.ParameterCode == parameterCode, ct)
            ?? throw new InvalidOperationException($"Parameter '{parameterCode}' not found on scenario {scenarioId}.");

        var previousValue = param.ProposedValue;
        param.ProposedValue = newProposedValue;

        await db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            scenarioId, regulatorId, Guid.NewGuid(),
            "ParameterUpdated",
            new { parameterCode, previousValue, newProposedValue },
            userId, ct);

        _log.LogInformation(
            "Updated parameter {ParameterCode} on Scenario={ScenarioId}: {PreviousValue} -> {NewValue}",
            parameterCode, scenarioId, previousValue, newProposedValue);
    }

    // ── Read ──────────────────────────────────────────────────────────

    public async Task<PolicyScenarioDetail> GetScenarioAsync(
        long scenarioId,
        int regulatorId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var scenario = await db.PolicyScenarios
            .AsNoTracking()
            .Include(s => s.Parameters)
            .Include(s => s.ImpactRuns)
            .Include(s => s.Decision)
            .Where(s => s.Id == scenarioId && s.RegulatorId == regulatorId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Scenario {scenarioId} not found for regulator {regulatorId}.");

        var parameters = scenario.Parameters
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PolicyParameterChange(
                p.ParameterCode,
                p.ParameterName,
                p.CurrentValue,
                p.ProposedValue,
                p.Unit,
                p.ReturnLineReference))
            .ToList();

        var runs = scenario.ImpactRuns
            .OrderByDescending(r => r.RunNumber)
            .Select(r => new PolicyScenarioRunSummary(
                r.Id,
                r.RunNumber,
                r.Status,
                r.TotalEntitiesEvaluated,
                r.EntitiesWouldBreach,
                r.EntitiesAlreadyBreaching,
                r.AggregateCapitalShortfall,
                r.CompletedAt))
            .ToList();

        return new PolicyScenarioDetail(
            scenario.Id,
            scenario.RegulatorId,
            scenario.Title,
            scenario.Description,
            scenario.PolicyDomain,
            scenario.TargetEntityTypes,
            scenario.BaselineDate,
            scenario.Status,
            scenario.Version,
            parameters,
            runs,
            scenario.Decision?.Id);
    }

    public async Task<PagedResult<PolicyScenarioSummary>> ListScenariosAsync(
        int regulatorId,
        PolicyDomain? domain,
        PolicyStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.PolicyScenarios
            .AsNoTracking()
            .Where(s => s.RegulatorId == regulatorId);

        if (domain.HasValue)
            query = query.Where(s => s.PolicyDomain == domain.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        try
        {
            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new PolicyScenarioSummary(
                    s.Id,
                    s.Title,
                    s.PolicyDomain,
                    s.Status,
                    s.TargetEntityTypes,
                    s.BaselineDate,
                    s.Parameters.Count,
                    s.ImpactRuns.Count,
                    s.CreatedAt))
                .ToListAsync(ct);

            return new PagedResult<PolicyScenarioSummary>(items, totalCount, page, pageSize);
        }
        catch (Exception ex) when (ex.IsMissingSchemaObject())
        {
            _log.LogWarning(
                ex,
                "Policy simulation schema is unavailable while listing scenarios for regulator {RegulatorId}. Returning an empty result set.",
                regulatorId);

            return new PagedResult<PolicyScenarioSummary>([], 0, page, pageSize);
        }
    }

    // ── Status Counts ─────────────────────────────────────────────────

    public async Task<ScenarioStatusCounts> GetStatusCountsAsync(
        int regulatorId,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var counts = await db.PolicyScenarios
                .AsNoTracking()
                .Where(s => s.RegulatorId == regulatorId)
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int Get(PolicyStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
            var total = counts.Sum(c => c.Count);

            return new ScenarioStatusCounts(
                total,
                Get(PolicyStatus.Draft),
                Get(PolicyStatus.ParametersSet),
                Get(PolicyStatus.Simulated),
                Get(PolicyStatus.Consultation) + Get(PolicyStatus.FeedbackClosed),
                Get(PolicyStatus.Enacted),
                Get(PolicyStatus.Withdrawn));
        }
        catch (Exception ex) when (ex.IsMissingSchemaObject())
        {
            return new ScenarioStatusCounts(0, 0, 0, 0, 0, 0, 0);
        }
    }

    // ── Clone ─────────────────────────────────────────────────────────

    public async Task<long> CloneScenarioAsync(
        long sourceScenarioId,
        int regulatorId,
        string newTitle,
        int userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var source = await db.PolicyScenarios
            .AsNoTracking()
            .Include(s => s.Parameters)
            .Where(s => s.Id == sourceScenarioId && s.RegulatorId == regulatorId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Scenario {sourceScenarioId} not found for regulator {regulatorId}.");

        var clone = new PolicyScenario
        {
            RegulatorId = regulatorId,
            Title = newTitle,
            Description = source.Description,
            PolicyDomain = source.PolicyDomain,
            TargetEntityTypes = source.TargetEntityTypes,
            BaselineDate = source.BaselineDate,
            Status = PolicyStatus.Draft,
            Version = 1,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.PolicyScenarios.Add(clone);
        await db.SaveChangesAsync(ct);

        foreach (var srcParam in source.Parameters.OrderBy(p => p.DisplayOrder))
        {
            db.PolicyParameters.Add(new PolicyParameter
            {
                ScenarioId = clone.Id,
                ParameterCode = srcParam.ParameterCode,
                ParameterName = srcParam.ParameterName,
                CurrentValue = srcParam.CurrentValue,
                ProposedValue = srcParam.ProposedValue,
                Unit = srcParam.Unit,
                ApplicableEntityTypes = srcParam.ApplicableEntityTypes,
                ReturnLineReference = srcParam.ReturnLineReference,
                DisplayOrder = srcParam.DisplayOrder,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (clone.Parameters.Count > 0 || source.Parameters.Count > 0)
        {
            clone.Status = PolicyStatus.ParametersSet;
            clone.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            clone.Id, regulatorId, Guid.NewGuid(),
            "ScenarioCloned",
            new { sourceScenarioId, newTitle, parameterCount = source.Parameters.Count },
            userId, ct);

        _log.LogInformation(
            "Cloned Scenario={SourceId} -> {CloneId} '{Title}' for Regulator={RegulatorId}",
            sourceScenarioId, clone.Id, newTitle, regulatorId);

        return clone.Id;
    }

    // ── Status Transition ─────────────────────────────────────────────

    public async Task TransitionStatusAsync(
        long scenarioId,
        int regulatorId,
        PolicyStatus newStatus,
        int userId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var scenario = await db.PolicyScenarios
            .FirstOrDefaultAsync(s => s.Id == scenarioId && s.RegulatorId == regulatorId, ct)
            ?? throw new InvalidOperationException($"Scenario {scenarioId} not found for regulator {regulatorId}.");

        var previousStatus = scenario.Status;
        scenario.Status = newStatus;
        scenario.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            scenarioId, regulatorId, Guid.NewGuid(),
            "StatusTransition",
            new { previousStatus = previousStatus.ToString(), newStatus = newStatus.ToString() },
            userId, ct);

        _log.LogInformation(
            "Scenario={ScenarioId} status: {PreviousStatus} -> {NewStatus} by User={UserId}",
            scenarioId, previousStatus, newStatus, userId);
    }
}
