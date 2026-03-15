using System.Text.Json;
using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class SanctionsWatchlistCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MetadataDbContext _db;

    public SanctionsWatchlistCatalogService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<SanctionsCatalogMaterializationResult> MaterializeAsync(
        SanctionsCatalogMaterializationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await EnsureStoreAsync(ct);

        var materializedAt = DateTime.UtcNow;
        var entries = BuildEntryRecords(request.Entries, materializedAt);
        var entryCountBySource = entries
            .GroupBy(x => x.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var sources = BuildSourceRecords(request.Sources, entryCountBySource, materializedAt);

        await ClearExistingCatalogAsync(ct);

        _db.SanctionsCatalogSources.AddRange(sources);
        _db.SanctionsCatalogEntries.AddRange(entries);
        await _db.SaveChangesAsync(ct);

        return new SanctionsCatalogMaterializationResult
        {
            SourceCount = sources.Count,
            EntryCount = entries.Count,
            MaterializedAt = materializedAt,
            Sources = sources
                .OrderBy(x => x.SourceCode, StringComparer.OrdinalIgnoreCase)
                .Select(x => new SanctionsCatalogSourceSummary
                {
                    SourceCode = x.SourceCode,
                    SourceName = x.SourceName,
                    RefreshCadence = x.RefreshCadence,
                    Status = x.Status,
                    EntryCount = x.EntryCount,
                    MaterializedAt = x.MaterializedAt
                })
                .ToList()
        };
    }

    public async Task<SanctionsCatalogState> LoadAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var sources = await _db.SanctionsCatalogSources
            .AsNoTracking()
            .OrderBy(x => x.SourceCode)
            .ToListAsync(ct);

        var entries = await _db.SanctionsCatalogEntries
            .AsNoTracking()
            .OrderBy(x => x.SourceCode)
            .ThenBy(x => x.PrimaryName)
            .ToListAsync(ct);

        return new SanctionsCatalogState
        {
            MaterializedAt = sources
                .OrderByDescending(x => x.MaterializedAt)
                .Select(x => (DateTime?)x.MaterializedAt)
                .FirstOrDefault(),
            Sources = sources
                .Select(x => new SanctionsCatalogSourceState
                {
                    SourceCode = x.SourceCode,
                    SourceName = x.SourceName,
                    RefreshCadence = x.RefreshCadence,
                    Status = x.Status,
                    EntryCount = x.EntryCount,
                    MaterializedAt = x.MaterializedAt
                })
                .ToList(),
            Entries = entries
                .Select(x => new SanctionsCatalogEntryState
                {
                    SourceCode = x.SourceCode,
                    PrimaryName = x.PrimaryName,
                    Aliases = DeserializeAliases(x.AliasesJson),
                    Category = x.Category,
                    RiskLevel = x.RiskLevel,
                    MaterializedAt = x.MaterializedAt
                })
                .ToList()
        };
    }

    private async Task EnsureStoreAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlServer())
        {
            throw new NotSupportedException(
                "Sanctions watchlist catalog store requires SQL Server. " +
                "The application is configured exclusively for SQL Server; " +
                "ensure the 'FcEngine' connection string points to a SQL Server instance " +
                "and that EF migrations have been applied via the Migrator.");
        }

        const string sql = """
            IF SCHEMA_ID(N'meta') IS NULL
                EXEC(N'CREATE SCHEMA [meta]');

            IF OBJECT_ID(N'[meta].[sanctions_watchlist_sources]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[sanctions_watchlist_sources]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [SourceCode] NVARCHAR(40) NOT NULL,
                    [SourceName] NVARCHAR(240) NOT NULL,
                    [RefreshCadence] NVARCHAR(40) NOT NULL,
                    [Status] NVARCHAR(30) NOT NULL,
                    [EntryCount] INT NOT NULL,
                    [MaterializedAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_sanctions_watchlist_sources_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_sanctions_watchlist_sources_SourceCode]
                    ON [meta].[sanctions_watchlist_sources]([SourceCode]);
                CREATE INDEX [IX_sanctions_watchlist_sources_MaterializedAt]
                    ON [meta].[sanctions_watchlist_sources]([MaterializedAt]);
                CREATE INDEX [IX_sanctions_watchlist_sources_Status]
                    ON [meta].[sanctions_watchlist_sources]([Status]);
            END;

            IF OBJECT_ID(N'[meta].[sanctions_watchlist_entries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[sanctions_watchlist_entries]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [EntryKey] NVARCHAR(240) NOT NULL,
                    [SourceCode] NVARCHAR(40) NOT NULL,
                    [PrimaryName] NVARCHAR(240) NOT NULL,
                    [AliasesJson] NVARCHAR(MAX) NOT NULL,
                    [Category] NVARCHAR(40) NOT NULL,
                    [RiskLevel] NVARCHAR(30) NOT NULL,
                    [MaterializedAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_sanctions_watchlist_entries_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_sanctions_watchlist_entries_EntryKey]
                    ON [meta].[sanctions_watchlist_entries]([EntryKey]);
                CREATE INDEX [IX_sanctions_watchlist_entries_SourceCode]
                    ON [meta].[sanctions_watchlist_entries]([SourceCode]);
                CREATE INDEX [IX_sanctions_watchlist_entries_Category]
                    ON [meta].[sanctions_watchlist_entries]([Category]);
                CREATE INDEX [IX_sanctions_watchlist_entries_MaterializedAt]
                    ON [meta].[sanctions_watchlist_entries]([MaterializedAt]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task ClearExistingCatalogAsync(CancellationToken ct)
    {
        if (_db.Database.IsSqlServer())
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM [meta].[sanctions_watchlist_entries];
                DELETE FROM [meta].[sanctions_watchlist_sources];
                """,
                ct);
            return;
        }

        var existingEntries = await _db.SanctionsCatalogEntries.ToListAsync(ct);
        var existingSources = await _db.SanctionsCatalogSources.ToListAsync(ct);

        if (existingEntries.Count > 0)
        {
            _db.SanctionsCatalogEntries.RemoveRange(existingEntries);
        }

        if (existingSources.Count > 0)
        {
            _db.SanctionsCatalogSources.RemoveRange(existingSources);
        }

        if (existingEntries.Count > 0 || existingSources.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static List<SanctionsCatalogSourceRecord> BuildSourceRecords(
        IReadOnlyList<SanctionsCatalogSourceInput> sources,
        IReadOnlyDictionary<string, int> entryCountBySource,
        DateTime materializedAt)
    {
        return sources
            .GroupBy(x => x.SourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.SourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SanctionsCatalogSourceRecord
            {
                SourceCode = x.SourceCode.Trim(),
                SourceName = x.SourceName.Trim(),
                RefreshCadence = x.RefreshCadence.Trim(),
                Status = x.Status.Trim(),
                EntryCount = entryCountBySource.TryGetValue(x.SourceCode, out var count) ? count : 0,
                MaterializedAt = materializedAt,
                CreatedAt = materializedAt
            })
            .ToList();
    }

    private static List<SanctionsCatalogEntryRecord> BuildEntryRecords(
        IReadOnlyList<SanctionsCatalogEntryInput> entries,
        DateTime materializedAt)
    {
        return entries
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceCode) && !string.IsNullOrWhiteSpace(x.PrimaryName))
            .GroupBy(x => BuildEntryKey(x.SourceCode, x.PrimaryName), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.SourceCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrimaryName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SanctionsCatalogEntryRecord
            {
                EntryKey = BuildEntryKey(x.SourceCode, x.PrimaryName),
                SourceCode = x.SourceCode.Trim(),
                PrimaryName = x.PrimaryName.Trim(),
                AliasesJson = JsonSerializer.Serialize(
                    x.Aliases
                        .Where(alias => !string.IsNullOrWhiteSpace(alias))
                        .Select(alias => alias.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    JsonOptions),
                Category = x.Category.Trim(),
                RiskLevel = x.RiskLevel.Trim(),
                MaterializedAt = materializedAt,
                CreatedAt = materializedAt
            })
            .ToList();
    }

    private static string BuildEntryKey(string sourceCode, string primaryName) =>
        $"{Normalize(sourceCode)}:{Normalize(primaryName)}";

    private static List<string> DeserializeAliases(string aliasesJson)
    {
        if (string.IsNullOrWhiteSpace(aliasesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(aliasesJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Normalize(string value) =>
        new string((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}

public sealed class SanctionsCatalogMaterializationRequest
{
    public List<SanctionsCatalogSourceInput> Sources { get; init; } = [];
    public List<SanctionsCatalogEntryInput> Entries { get; init; } = [];
}

public sealed class SanctionsCatalogSourceInput
{
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string RefreshCadence { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class SanctionsCatalogEntryInput
{
    public string SourceCode { get; init; } = string.Empty;
    public string PrimaryName { get; init; } = string.Empty;
    public List<string> Aliases { get; init; } = [];
    public string Category { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
}

public sealed class SanctionsCatalogMaterializationResult
{
    public int SourceCount { get; init; }
    public int EntryCount { get; init; }
    public DateTime MaterializedAt { get; init; }
    public List<SanctionsCatalogSourceSummary> Sources { get; init; } = [];
}

public sealed class SanctionsCatalogState
{
    public DateTime? MaterializedAt { get; init; }
    public List<SanctionsCatalogSourceState> Sources { get; init; } = [];
    public List<SanctionsCatalogEntryState> Entries { get; init; } = [];
}

public sealed class SanctionsCatalogSourceSummary
{
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string RefreshCadence { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int EntryCount { get; init; }
    public DateTime MaterializedAt { get; init; }
}

public sealed class SanctionsCatalogSourceState
{
    public string SourceCode { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string RefreshCadence { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int EntryCount { get; init; }
    public DateTime MaterializedAt { get; init; }
}

public sealed class SanctionsCatalogEntryState
{
    public string SourceCode { get; init; } = string.Empty;
    public string PrimaryName { get; init; } = string.Empty;
    public List<string> Aliases { get; init; } = [];
    public string Category { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public DateTime MaterializedAt { get; init; }
}
