using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Audit;
using FC.Engine.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Audit;

public class ReturnTimelineServiceTests
{
    [Fact]
    public async Task Timeline_Includes_Audit_Entries_And_Field_Changes()
    {
        await using var db = CreateDb(nameof(Timeline_Includes_Audit_Entries_And_Field_Changes));
        var tenantId = Guid.NewGuid();
        var submissionId = 42;

        // Seed audit entries
        db.AuditLog.Add(new AuditLogEntry
        {
            TenantId = tenantId,
            EntityType = "Submission",
            EntityId = submissionId,
            Action = "Create",
            PerformedBy = "maker1",
            PerformedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            Hash = "abc",
            PreviousHash = "GENESIS",
            SequenceNumber = 1
        });
        db.AuditLog.Add(new AuditLogEntry
        {
            TenantId = tenantId,
            EntityType = "Submission",
            EntityId = submissionId,
            Action = "Submit",
            PerformedBy = "maker1",
            PerformedAt = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            Hash = "def",
            PreviousHash = "abc",
            SequenceNumber = 2
        });

        // Seed field change
        db.FieldChangeHistory.Add(new FieldChangeHistory
        {
            TenantId = tenantId,
            SubmissionId = submissionId,
            ReturnCode = "CBN_MBR",
            FieldName = "total_assets",
            OldValue = "1000",
            NewValue = "1500",
            ChangeSource = "Manual",
            ChangedBy = "maker1",
            ChangedAt = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync();

        var sut = new ReturnTimelineService(db);
        var events = await sut.GetTimelineAsync(submissionId);

        events.Should().HaveCount(3);
        events[0].EventType.Should().Be("Created");
        events[1].EventType.Should().Be("FieldChanged");
        events[2].EventType.Should().Be("Submitted");
    }

    [Fact]
    public async Task Events_Sorted_Chronologically()
    {
        await using var db = CreateDb(nameof(Events_Sorted_Chronologically));
        var tenantId = Guid.NewGuid();
        var submissionId = 99;

        db.AuditLog.Add(new AuditLogEntry
        {
            TenantId = tenantId,
            EntityType = "Submission",
            EntityId = submissionId,
            Action = "Submit",
            PerformedBy = "maker1",
            PerformedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            Hash = "h2",
            PreviousHash = "h1",
            SequenceNumber = 2
        });
        db.AuditLog.Add(new AuditLogEntry
        {
            TenantId = tenantId,
            EntityType = "Submission",
            EntityId = submissionId,
            Action = "Create",
            PerformedBy = "maker1",
            PerformedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Hash = "h1",
            PreviousHash = "GENESIS",
            SequenceNumber = 1
        });

        await db.SaveChangesAsync();

        var sut = new ReturnTimelineService(db);
        var events = await sut.GetTimelineAsync(submissionId);

        events.Should().HaveCount(2);
        events[0].Timestamp.Should().BeBefore(events[1].Timestamp);
    }

    [Fact]
    public async Task FieldChanged_Events_Include_Diff()
    {
        await using var db = CreateDb(nameof(FieldChanged_Events_Include_Diff));
        var tenantId = Guid.NewGuid();
        var submissionId = 7;

        db.FieldChangeHistory.Add(new FieldChangeHistory
        {
            TenantId = tenantId,
            SubmissionId = submissionId,
            ReturnCode = "CBN_MBR",
            FieldName = "capital_ratio",
            OldValue = "0.08",
            NewValue = "0.12",
            ChangeSource = "Manual",
            ChangedBy = "analyst1",
            ChangedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new ReturnTimelineService(db);
        var events = await sut.GetTimelineAsync(submissionId);

        events.Should().ContainSingle();
        var evt = events[0];
        evt.Diff.Should().NotBeNull();
        evt.Diff!["field"].Should().Be("capital_ratio");
        evt.Diff["before"].Should().Be("0.08");
        evt.Diff["after"].Should().Be("0.12");
    }

    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }
}
