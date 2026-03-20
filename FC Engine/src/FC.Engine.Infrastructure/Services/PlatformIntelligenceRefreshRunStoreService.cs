using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class PlatformIntelligenceRefreshRunStoreService
{
    private readonly MetadataDbContext _db;

    public PlatformIntelligenceRefreshRunStoreService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<PlatformIntelligenceRefreshRunState?> LoadLatestAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var latest = await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            return null;
        }

        var lastSuccessfulCompletedAtUtc = await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .Where(x => x.Succeeded)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => (DateTime?)x.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        var lastFailedCompletedAtUtc = await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .Where(x => !x.Succeeded)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => (DateTime?)x.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        return MapState(latest, lastSuccessfulCompletedAtUtc, lastFailedCompletedAtUtc);
    }

    public async Task<IReadOnlyList<PlatformIntelligenceRefreshRunState>> LoadRecentAsync(int take = 8, CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var rows = await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(ct);

        return rows
            .Select(x => MapState(x, null, null))
            .ToList();
    }

    public async Task<PlatformIntelligenceRefreshRunState> RecordSuccessAsync(
        PlatformIntelligenceRefreshRunSuccessCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await EnsureStoreAsync(ct);

        var record = new PlatformIntelligenceRefreshRunRecord
        {
            StartedAtUtc = command.StartedAtUtc,
            CompletedAtUtc = command.CompletedAtUtc,
            Succeeded = true,
            Status = "Healthy",
            GeneratedAtUtc = command.GeneratedAtUtc,
            DurationMilliseconds = command.DurationMilliseconds,
            InstitutionCount = command.InstitutionCount,
            InterventionCount = command.InterventionCount,
            TimelineCount = command.TimelineCount,
            DashboardPacksMaterialized = command.DashboardPacksMaterialized,
            RolloutCatalogMaterializedAt = command.RolloutCatalogMaterializedAt,
            KnowledgeCatalogMaterializedAt = command.KnowledgeCatalogMaterializedAt,
            KnowledgeDossierMaterializedAt = command.KnowledgeDossierMaterializedAt,
            CapitalPackMaterializedAt = command.CapitalPackMaterializedAt,
            SanctionsPackMaterializedAt = command.SanctionsPackMaterializedAt,
            SanctionsStrDraftCatalogMaterializedAt = command.SanctionsStrDraftCatalogMaterializedAt,
            ResiliencePackMaterializedAt = command.ResiliencePackMaterializedAt,
            ModelRiskPackMaterializedAt = command.ModelRiskPackMaterializedAt,
            CreatedAt = command.CompletedAtUtc
        };

        _db.PlatformIntelligenceRefreshRuns.Add(record);
        await _db.SaveChangesAsync(ct);

        return MapState(record, record.CompletedAtUtc, await LoadLatestFailureCompletedAtUtcAsync(ct));
    }

    public async Task<PlatformIntelligenceRefreshRunState> RecordFailureAsync(
        PlatformIntelligenceRefreshRunFailureCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await EnsureStoreAsync(ct);

        var record = new PlatformIntelligenceRefreshRunRecord
        {
            StartedAtUtc = command.StartedAtUtc,
            CompletedAtUtc = command.CompletedAtUtc,
            Succeeded = false,
            Status = "Failed",
            FailureMessage = TrimFailureMessage(command.FailureMessage),
            DurationMilliseconds = command.DurationMilliseconds,
            InstitutionCount = 0,
            InterventionCount = 0,
            TimelineCount = 0,
            DashboardPacksMaterialized = 0,
            CreatedAt = command.CompletedAtUtc
        };

        _db.PlatformIntelligenceRefreshRuns.Add(record);
        await _db.SaveChangesAsync(ct);

        return MapState(record, await LoadLatestSuccessCompletedAtUtcAsync(ct), record.CompletedAtUtc);
    }

    private async Task<DateTime?> LoadLatestSuccessCompletedAtUtcAsync(CancellationToken ct) =>
        await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .Where(x => x.Succeeded)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => (DateTime?)x.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<DateTime?> LoadLatestFailureCompletedAtUtcAsync(CancellationToken ct) =>
        await _db.PlatformIntelligenceRefreshRuns
            .AsNoTracking()
            .Where(x => !x.Succeeded)
            .OrderByDescending(x => x.CompletedAtUtc)
            .Select(x => (DateTime?)x.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

    private static PlatformIntelligenceRefreshRunState MapState(
        PlatformIntelligenceRefreshRunRecord record,
        DateTime? lastSuccessfulCompletedAtUtc,
        DateTime? lastFailedCompletedAtUtc) =>
        new()
        {
            StartedAtUtc = record.StartedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            Succeeded = record.Succeeded,
            Status = record.Status,
            FailureMessage = record.FailureMessage,
            GeneratedAtUtc = record.GeneratedAtUtc,
            DurationMilliseconds = record.DurationMilliseconds,
            InstitutionCount = record.InstitutionCount,
            InterventionCount = record.InterventionCount,
            TimelineCount = record.TimelineCount,
            DashboardPacksMaterialized = record.DashboardPacksMaterialized,
            LastSuccessfulCompletedAtUtc = lastSuccessfulCompletedAtUtc,
            LastFailedCompletedAtUtc = lastFailedCompletedAtUtc
        };

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            IF SCHEMA_ID(N'meta') IS NULL
                EXEC(N'CREATE SCHEMA [meta]');

            IF OBJECT_ID(N'[meta].[platform_intelligence_refresh_runs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[platform_intelligence_refresh_runs]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [StartedAtUtc] DATETIME2 NOT NULL,
                    [CompletedAtUtc] DATETIME2 NOT NULL,
                    [Succeeded] BIT NOT NULL,
                    [Status] NVARCHAR(30) NOT NULL,
                    [FailureMessage] NVARCHAR(1200) NULL,
                    [GeneratedAtUtc] DATETIME2 NULL,
                    [DurationMilliseconds] INT NOT NULL,
                    [InstitutionCount] INT NOT NULL,
                    [InterventionCount] INT NOT NULL,
                    [TimelineCount] INT NOT NULL,
                    [DashboardPacksMaterialized] INT NOT NULL,
                    [RolloutCatalogMaterializedAt] DATETIME2 NULL,
                    [KnowledgeCatalogMaterializedAt] DATETIME2 NULL,
                    [KnowledgeDossierMaterializedAt] DATETIME2 NULL,
                    [CapitalPackMaterializedAt] DATETIME2 NULL,
                    [SanctionsPackMaterializedAt] DATETIME2 NULL,
                    [SanctionsStrDraftCatalogMaterializedAt] DATETIME2 NULL,
                    [ResiliencePackMaterializedAt] DATETIME2 NULL,
                    [ModelRiskPackMaterializedAt] DATETIME2 NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_platform_intelligence_refresh_runs_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX [IX_platform_intelligence_refresh_runs_CompletedAtUtc]
                    ON [meta].[platform_intelligence_refresh_runs]([CompletedAtUtc]);
                CREATE INDEX [IX_platform_intelligence_refresh_runs_Succeeded]
                    ON [meta].[platform_intelligence_refresh_runs]([Succeeded]);
                CREATE INDEX [IX_platform_intelligence_refresh_runs_Status]
                    ON [meta].[platform_intelligence_refresh_runs]([Status]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static string? TrimFailureMessage(string? failureMessage)
    {
        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            return null;
        }

        return failureMessage.Length <= 1200
            ? failureMessage
            : failureMessage[..1200];
    }
}

public sealed class PlatformIntelligenceRefreshRunSuccessCommand
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public int DurationMilliseconds { get; init; }
    public int InstitutionCount { get; init; }
    public int InterventionCount { get; init; }
    public int TimelineCount { get; init; }
    public int DashboardPacksMaterialized { get; init; }
    public DateTime? RolloutCatalogMaterializedAt { get; init; }
    public DateTime? KnowledgeCatalogMaterializedAt { get; init; }
    public DateTime? KnowledgeDossierMaterializedAt { get; init; }
    public DateTime? CapitalPackMaterializedAt { get; init; }
    public DateTime? SanctionsPackMaterializedAt { get; init; }
    public DateTime? SanctionsStrDraftCatalogMaterializedAt { get; init; }
    public DateTime? ResiliencePackMaterializedAt { get; init; }
    public DateTime? ModelRiskPackMaterializedAt { get; init; }
}

public sealed class PlatformIntelligenceRefreshRunFailureCommand
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public int DurationMilliseconds { get; init; }
    public string? FailureMessage { get; init; }
}

public sealed class PlatformIntelligenceRefreshRunState
{
    public DateTime StartedAtUtc { get; init; }
    public DateTime CompletedAtUtc { get; init; }
    public bool Succeeded { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? FailureMessage { get; init; }
    public DateTime? GeneratedAtUtc { get; init; }
    public int DurationMilliseconds { get; init; }
    public int InstitutionCount { get; init; }
    public int InterventionCount { get; init; }
    public int TimelineCount { get; init; }
    public int DashboardPacksMaterialized { get; init; }
    public DateTime? LastSuccessfulCompletedAtUtc { get; init; }
    public DateTime? LastFailedCompletedAtUtc { get; init; }
}
