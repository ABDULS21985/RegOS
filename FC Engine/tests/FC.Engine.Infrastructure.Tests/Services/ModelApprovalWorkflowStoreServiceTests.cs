using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class ModelApprovalWorkflowStoreServiceTests
{
    [Fact]
    public async Task RecordStageChangeAsync_Persists_Latest_Stage_And_Audit_Trail()
    {
        await using var db = CreateDb();
        var sut = new ModelApprovalWorkflowStoreService(db);

        await sut.RecordStageChangeAsync(new ModelApprovalWorkflowCommand
        {
            WorkflowKey = "CAR|formula:car_ratio|638771234000000000",
            ModelCode = "CAR",
            ModelName = "Capital Adequacy Ratio Engine",
            Artifact = "formula:car_ratio",
            PreviousStage = "Model Owner",
            Stage = "Validation Team",
            ChangedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        await sut.RecordStageChangeAsync(new ModelApprovalWorkflowCommand
        {
            WorkflowKey = "CAR|formula:car_ratio|638771234000000000",
            ModelCode = "CAR",
            ModelName = "Capital Adequacy Ratio Engine",
            Artifact = "formula:car_ratio",
            PreviousStage = "Validation Team",
            Stage = "Board Review",
            ChangedAtUtc = DateTime.UtcNow
        });

        var state = await sut.LoadAsync();

        state.Stages.Should().ContainSingle();
        state.Stages[0].Stage.Should().Be("Board Review");
        state.AuditTrail.Should().HaveCount(2);
        state.AuditTrail[0].Stage.Should().Be("Board Review");

        var persistedState = await db.ModelApprovalWorkflowStates.AsNoTracking().ToListAsync();
        var persistedAudit = await db.ModelApprovalAuditRecords.AsNoTracking().ToListAsync();

        persistedState.Should().ContainSingle();
        persistedState[0].Stage.Should().Be("Board Review");
        persistedAudit.Should().HaveCount(2);
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
