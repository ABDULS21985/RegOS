using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class ResilienceAssessmentStoreService
{
    private readonly MetadataDbContext _db;

    public ResilienceAssessmentStoreService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<ResilienceAssessmentState> LoadAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var responses = await _db.ResilienceAssessmentResponses
            .AsNoTracking()
            .OrderBy(x => x.Domain)
            .ThenBy(x => x.QuestionId)
            .ToListAsync(ct);

        return new ResilienceAssessmentState
        {
            Responses = responses
                .Select(x => new ResilienceAssessmentResponseState
                {
                    QuestionId = x.QuestionId,
                    Domain = x.Domain,
                    Prompt = x.Prompt,
                    Score = x.Score,
                    AnsweredAtUtc = x.AnsweredAtUtc
                })
                .ToList()
        };
    }

    public async Task RecordResponseAsync(ResilienceAssessmentResponseCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await EnsureStoreAsync(ct);

        var questionId = command.QuestionId.Trim();
        var existing = await _db.ResilienceAssessmentResponses
            .FirstOrDefaultAsync(x => x.QuestionId == questionId, ct);

        if (command.Score <= 0)
        {
            if (existing is not null)
            {
                _db.ResilienceAssessmentResponses.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }

            return;
        }

        var answeredAtUtc = command.AnsweredAtUtc == default ? DateTime.UtcNow : command.AnsweredAtUtc;
        if (existing is null)
        {
            _db.ResilienceAssessmentResponses.Add(new ResilienceAssessmentResponseRecord
            {
                QuestionId = questionId,
                Domain = command.Domain,
                Prompt = command.Prompt,
                Score = command.Score,
                AnsweredAtUtc = answeredAtUtc,
                CreatedAt = answeredAtUtc
            });
        }
        else
        {
            existing.Domain = command.Domain;
            existing.Prompt = command.Prompt;
            existing.Score = command.Score;
            existing.AnsweredAtUtc = answeredAtUtc;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var existing = await _db.ResilienceAssessmentResponses.ToListAsync(ct);
        if (existing.Count == 0)
        {
            return;
        }

        _db.ResilienceAssessmentResponses.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);
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

            IF OBJECT_ID(N'[meta].[resilience_self_assessment_responses]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[resilience_self_assessment_responses]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [QuestionId] NVARCHAR(120) NOT NULL,
                    [Domain] NVARCHAR(120) NOT NULL,
                    [Prompt] NVARCHAR(600) NOT NULL,
                    [Score] INT NOT NULL,
                    [AnsweredAtUtc] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_resilience_self_assessment_responses_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_resilience_self_assessment_responses_QuestionId]
                    ON [meta].[resilience_self_assessment_responses]([QuestionId]);
                CREATE INDEX [IX_resilience_self_assessment_responses_Domain]
                    ON [meta].[resilience_self_assessment_responses]([Domain]);
                CREATE INDEX [IX_resilience_self_assessment_responses_AnsweredAtUtc]
                    ON [meta].[resilience_self_assessment_responses]([AnsweredAtUtc]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}

public sealed class ResilienceAssessmentState
{
    public List<ResilienceAssessmentResponseState> Responses { get; init; } = [];
}

public sealed class ResilienceAssessmentResponseState
{
    public string QuestionId { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public int Score { get; init; }
    public DateTime AnsweredAtUtc { get; init; }
}

public sealed class ResilienceAssessmentResponseCommand
{
    public string QuestionId { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Prompt { get; init; } = string.Empty;
    public int Score { get; init; }
    public DateTime AnsweredAtUtc { get; init; }
}
