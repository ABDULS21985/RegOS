using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class SanctionsWorkflowStoreService
{
    private const int MaxFalsePositiveEntries = 80;
    private const int MaxAuditEntries = 120;

    private readonly MetadataDbContext _db;

    public SanctionsWorkflowStoreService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<SanctionsWorkflowState> LoadAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var falsePositiveLibrary = await _db.SanctionsFalsePositiveEntries
            .AsNoTracking()
            .OrderByDescending(x => x.ReviewedAtUtc)
            .Take(MaxFalsePositiveEntries)
            .ToListAsync(ct);

        var auditTrail = await _db.SanctionsDecisionAuditRecords
            .AsNoTracking()
            .OrderByDescending(x => x.ReviewedAtUtc)
            .Take(MaxAuditEntries)
            .ToListAsync(ct);

        var latestDecisions = auditTrail
            .GroupBy(x => x.MatchKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.ReviewedAtUtc)
                .First())
            .OrderByDescending(x => x.ReviewedAtUtc)
            .Select(x => new SanctionsWorkflowDecisionState
            {
                MatchKey = x.MatchKey,
                Decision = x.Decision,
                ReviewedAtUtc = x.ReviewedAtUtc
            })
            .ToList();

        return new SanctionsWorkflowState
        {
            FalsePositiveLibrary = falsePositiveLibrary
                .Select(x => new SanctionsWorkflowFalsePositiveRecord
                {
                    MatchKey = x.MatchKey,
                    Subject = x.Subject,
                    MatchedName = x.MatchedName,
                    SourceCode = x.SourceCode,
                    RiskLevel = x.RiskLevel,
                    ReviewedAtUtc = x.ReviewedAtUtc
                })
                .ToList(),
            AuditTrail = auditTrail
                .Select(x => new SanctionsWorkflowAuditRecord
                {
                    MatchKey = x.MatchKey,
                    Subject = x.Subject,
                    MatchedName = x.MatchedName,
                    SourceCode = x.SourceCode,
                    PreviousDecision = x.PreviousDecision,
                    Decision = x.Decision,
                    ReviewedAtUtc = x.ReviewedAtUtc
                })
                .ToList(),
            LatestDecisions = latestDecisions
        };
    }

    public async Task RecordDecisionAsync(SanctionsWorkflowDecisionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await EnsureStoreAsync(ct);

        var reviewedAtUtc = command.ReviewedAtUtc == default ? DateTime.UtcNow : command.ReviewedAtUtc;
        var matchKey = command.MatchKey.Trim();

        var existingFalsePositive = await _db.SanctionsFalsePositiveEntries
            .FirstOrDefaultAsync(x => x.MatchKey == matchKey, ct);

        if (string.Equals(command.Decision, "False Positive", StringComparison.OrdinalIgnoreCase))
        {
            if (existingFalsePositive is null)
            {
                _db.SanctionsFalsePositiveEntries.Add(new SanctionsFalsePositiveEntry
                {
                    MatchKey = matchKey,
                    Subject = command.Subject,
                    MatchedName = command.MatchedName,
                    SourceCode = command.SourceCode,
                    RiskLevel = command.RiskLevel,
                    ReviewedAtUtc = reviewedAtUtc,
                    CreatedAt = reviewedAtUtc
                });
            }
            else
            {
                existingFalsePositive.Subject = command.Subject;
                existingFalsePositive.MatchedName = command.MatchedName;
                existingFalsePositive.SourceCode = command.SourceCode;
                existingFalsePositive.RiskLevel = command.RiskLevel;
                existingFalsePositive.ReviewedAtUtc = reviewedAtUtc;
            }
        }
        else if (existingFalsePositive is not null)
        {
            _db.SanctionsFalsePositiveEntries.Remove(existingFalsePositive);
        }

        _db.SanctionsDecisionAuditRecords.Add(new SanctionsDecisionAuditRecord
        {
            MatchKey = matchKey,
            Subject = command.Subject,
            MatchedName = command.MatchedName,
            SourceCode = command.SourceCode,
            PreviousDecision = command.PreviousDecision,
            Decision = command.Decision,
            ReviewedAtUtc = reviewedAtUtc,
            CreatedAt = reviewedAtUtc
        });

        await _db.SaveChangesAsync(ct);
        await TrimStoreAsync(ct);
    }

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF SCHEMA_ID(N'meta') IS NULL
                EXEC(N'CREATE SCHEMA [meta]');

            IF OBJECT_ID(N'[meta].[sanctions_false_positive_library]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[sanctions_false_positive_library]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [MatchKey] NVARCHAR(240) NOT NULL,
                    [Subject] NVARCHAR(240) NOT NULL,
                    [MatchedName] NVARCHAR(240) NOT NULL,
                    [SourceCode] NVARCHAR(40) NOT NULL,
                    [RiskLevel] NVARCHAR(30) NOT NULL,
                    [ReviewedAtUtc] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_sanctions_false_positive_library_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_sanctions_false_positive_library_MatchKey]
                    ON [meta].[sanctions_false_positive_library]([MatchKey]);
                CREATE INDEX [IX_sanctions_false_positive_library_ReviewedAtUtc]
                    ON [meta].[sanctions_false_positive_library]([ReviewedAtUtc]);
                CREATE INDEX [IX_sanctions_false_positive_library_SourceCode]
                    ON [meta].[sanctions_false_positive_library]([SourceCode]);
            END;

            IF OBJECT_ID(N'[meta].[sanctions_decision_audit]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[sanctions_decision_audit]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [MatchKey] NVARCHAR(240) NOT NULL,
                    [Subject] NVARCHAR(240) NOT NULL,
                    [MatchedName] NVARCHAR(240) NOT NULL,
                    [SourceCode] NVARCHAR(40) NOT NULL,
                    [PreviousDecision] NVARCHAR(40) NOT NULL,
                    [Decision] NVARCHAR(40) NOT NULL,
                    [ReviewedAtUtc] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_sanctions_decision_audit_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX [IX_sanctions_decision_audit_MatchKey]
                    ON [meta].[sanctions_decision_audit]([MatchKey]);
                CREATE INDEX [IX_sanctions_decision_audit_ReviewedAtUtc]
                    ON [meta].[sanctions_decision_audit]([ReviewedAtUtc]);
                CREATE INDEX [IX_sanctions_decision_audit_Decision]
                    ON [meta].[sanctions_decision_audit]([Decision]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task TrimStoreAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer())
        {
            return;
        }

        // Raw SQL ensures idempotency under concurrent decisions: if another request
        // has already deleted the same overflow rows the statement affects 0 rows
        // rather than raising a DbUpdateConcurrencyException from the EF change tracker.
        var sql = $"""
            DELETE FROM [meta].[sanctions_false_positive_library]
            WHERE [Id] NOT IN (
                SELECT TOP ({MaxFalsePositiveEntries}) [Id]
                FROM [meta].[sanctions_false_positive_library]
                ORDER BY [ReviewedAtUtc] DESC
            );

            DELETE FROM [meta].[sanctions_decision_audit]
            WHERE [Id] NOT IN (
                SELECT TOP ({MaxAuditEntries}) [Id]
                FROM [meta].[sanctions_decision_audit]
                ORDER BY [ReviewedAtUtc] DESC
            );
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}

public sealed class SanctionsWorkflowState
{
    public List<SanctionsWorkflowFalsePositiveRecord> FalsePositiveLibrary { get; init; } = [];
    public List<SanctionsWorkflowAuditRecord> AuditTrail { get; init; } = [];
    public List<SanctionsWorkflowDecisionState> LatestDecisions { get; init; } = [];
}

public sealed class SanctionsWorkflowFalsePositiveRecord
{
    public string MatchKey { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string MatchedName { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public DateTime ReviewedAtUtc { get; init; }
}

public sealed class SanctionsWorkflowAuditRecord
{
    public string MatchKey { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string MatchedName { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string PreviousDecision { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public DateTime ReviewedAtUtc { get; init; }
}

public sealed class SanctionsWorkflowDecisionState
{
    public string MatchKey { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public DateTime ReviewedAtUtc { get; init; }
}

public sealed class SanctionsWorkflowDecisionCommand
{
    public string MatchKey { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string MatchedName { get; init; } = string.Empty;
    public string SourceCode { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public string PreviousDecision { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public DateTime ReviewedAtUtc { get; init; }
}
