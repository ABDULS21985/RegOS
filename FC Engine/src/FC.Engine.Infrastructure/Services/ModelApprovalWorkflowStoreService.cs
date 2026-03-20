using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Services;

public sealed class ModelApprovalWorkflowStoreService
{
    private const int MaxAuditEntries = 240;
    private readonly MetadataDbContext _db;

    public ModelApprovalWorkflowStoreService(MetadataDbContext db)
    {
        _db = db;
    }

    public async Task<ModelApprovalWorkflowState> LoadAsync(CancellationToken ct = default)
    {
        await EnsureStoreAsync(ct);

        var stages = await _db.ModelApprovalWorkflowStates
            .AsNoTracking()
            .OrderByDescending(x => x.ChangedAtUtc)
            .ToListAsync(ct);

        var auditTrail = await _db.ModelApprovalAuditRecords
            .AsNoTracking()
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(MaxAuditEntries)
            .ToListAsync(ct);

        return new ModelApprovalWorkflowState
        {
            Stages = stages
                .Select(x => new ModelApprovalWorkflowStageRecord
                {
                    WorkflowKey = x.WorkflowKey,
                    ModelCode = x.ModelCode,
                    ModelName = x.ModelName,
                    Artifact = x.Artifact,
                    Stage = x.Stage,
                    ChangedAtUtc = x.ChangedAtUtc
                })
                .ToList(),
            AuditTrail = auditTrail
                .Select(x => new ModelApprovalWorkflowAuditRecord
                {
                    WorkflowKey = x.WorkflowKey,
                    ModelCode = x.ModelCode,
                    ModelName = x.ModelName,
                    Artifact = x.Artifact,
                    PreviousStage = x.PreviousStage,
                    Stage = x.Stage,
                    ChangedAtUtc = x.ChangedAtUtc
                })
                .ToList()
        };
    }

    public async Task RecordStageChangeAsync(ModelApprovalWorkflowCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureStoreAsync(ct);

        var changedAtUtc = command.ChangedAtUtc == default ? DateTime.UtcNow : command.ChangedAtUtc;
        var workflowKey = command.WorkflowKey.Trim();

        var existingState = await _db.ModelApprovalWorkflowStates
            .FirstOrDefaultAsync(x => x.WorkflowKey == workflowKey, ct);

        if (existingState is null)
        {
            _db.ModelApprovalWorkflowStates.Add(new ModelApprovalWorkflowStateRecord
            {
                WorkflowKey = workflowKey,
                ModelCode = command.ModelCode,
                ModelName = command.ModelName,
                Artifact = command.Artifact,
                Stage = command.Stage,
                ChangedAtUtc = changedAtUtc,
                CreatedAt = changedAtUtc
            });
        }
        else
        {
            existingState.ModelCode = command.ModelCode;
            existingState.ModelName = command.ModelName;
            existingState.Artifact = command.Artifact;
            existingState.Stage = command.Stage;
            existingState.ChangedAtUtc = changedAtUtc;
        }

        _db.ModelApprovalAuditRecords.Add(new ModelApprovalAuditRecord
        {
            WorkflowKey = workflowKey,
            ModelCode = command.ModelCode,
            ModelName = command.ModelName,
            Artifact = command.Artifact,
            PreviousStage = command.PreviousStage,
            Stage = command.Stage,
            ChangedAtUtc = changedAtUtc,
            CreatedAt = changedAtUtc
        });

        await _db.SaveChangesAsync(ct);
        await TrimAuditAsync(ct);
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

            IF OBJECT_ID(N'[meta].[model_approval_states]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[model_approval_states]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [WorkflowKey] NVARCHAR(240) NOT NULL,
                    [ModelCode] NVARCHAR(60) NOT NULL,
                    [ModelName] NVARCHAR(200) NOT NULL,
                    [Artifact] NVARCHAR(240) NOT NULL,
                    [Stage] NVARCHAR(40) NOT NULL,
                    [ChangedAtUtc] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_model_approval_states_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX [IX_model_approval_states_WorkflowKey]
                    ON [meta].[model_approval_states]([WorkflowKey]);
                CREATE INDEX [IX_model_approval_states_Stage]
                    ON [meta].[model_approval_states]([Stage]);
                CREATE INDEX [IX_model_approval_states_ChangedAtUtc]
                    ON [meta].[model_approval_states]([ChangedAtUtc]);
            END;

            IF OBJECT_ID(N'[meta].[model_approval_audit]', N'U') IS NULL
            BEGIN
                CREATE TABLE [meta].[model_approval_audit]
                (
                    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [WorkflowKey] NVARCHAR(240) NOT NULL,
                    [ModelCode] NVARCHAR(60) NOT NULL,
                    [ModelName] NVARCHAR(200) NOT NULL,
                    [Artifact] NVARCHAR(240) NOT NULL,
                    [PreviousStage] NVARCHAR(40) NOT NULL,
                    [Stage] NVARCHAR(40) NOT NULL,
                    [ChangedAtUtc] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_model_approval_audit_CreatedAt] DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX [IX_model_approval_audit_WorkflowKey]
                    ON [meta].[model_approval_audit]([WorkflowKey]);
                CREATE INDEX [IX_model_approval_audit_Stage]
                    ON [meta].[model_approval_audit]([Stage]);
                CREATE INDEX [IX_model_approval_audit_ChangedAtUtc]
                    ON [meta].[model_approval_audit]([ChangedAtUtc]);
            END;
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task TrimAuditAsync(CancellationToken ct)
    {
        var staleEntries = await _db.ModelApprovalAuditRecords
            .OrderByDescending(x => x.ChangedAtUtc)
            .Skip(MaxAuditEntries)
            .ToListAsync(ct);

        if (staleEntries.Count == 0)
        {
            return;
        }

        _db.ModelApprovalAuditRecords.RemoveRange(staleEntries);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class ModelApprovalWorkflowState
{
    public List<ModelApprovalWorkflowStageRecord> Stages { get; init; } = [];
    public List<ModelApprovalWorkflowAuditRecord> AuditTrail { get; init; } = [];
}

public sealed class ModelApprovalWorkflowStageRecord
{
    public string WorkflowKey { get; init; } = string.Empty;
    public string ModelCode { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string Artifact { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public DateTime ChangedAtUtc { get; init; }
}

public sealed class ModelApprovalWorkflowAuditRecord
{
    public string WorkflowKey { get; init; } = string.Empty;
    public string ModelCode { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string Artifact { get; init; } = string.Empty;
    public string PreviousStage { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public DateTime ChangedAtUtc { get; init; }
}

public sealed class ModelApprovalWorkflowCommand
{
    public string WorkflowKey { get; init; } = string.Empty;
    public string ModelCode { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string Artifact { get; init; } = string.Empty;
    public string PreviousStage { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public DateTime ChangedAtUtc { get; init; }
}
