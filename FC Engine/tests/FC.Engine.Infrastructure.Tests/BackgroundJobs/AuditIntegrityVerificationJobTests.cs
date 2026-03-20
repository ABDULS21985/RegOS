using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Audit;
using FC.Engine.Infrastructure.BackgroundJobs;
using FC.Engine.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.BackgroundJobs;

public class AuditIntegrityVerificationJobTests
{
    [Fact]
    public async Task Valid_Chain_Passes_Verification()
    {
        await using var db = CreateDb(nameof(Valid_Chain_Passes_Verification));
        var tenantId = Guid.NewGuid();
        await SeedValidChain(db, tenantId);

        var notifier = new Mock<INotificationOrchestrator>();
        var sut = CreateJob(db, notifier.Object);

        await sut.VerifyTenantChain(db, notifier.Object, tenantId, CancellationToken.None);

        notifier.Verify(
            n => n.Notify(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tampered_Hash_Is_Detected()
    {
        await using var db = CreateDb(nameof(Tampered_Hash_Is_Detected));
        var tenantId = Guid.NewGuid();
        await SeedValidChain(db, tenantId);

        // Tamper with the second entry's hash
        var entry = await db.AuditLog.Where(e => e.SequenceNumber == 2).SingleAsync();
        entry.Hash = "tampered_hash_value_that_is_definitely_wrong_abc123";
        await db.SaveChangesAsync();

        var notifier = new Mock<INotificationOrchestrator>();
        var sut = CreateJob(db, notifier.Object);

        await sut.VerifyTenantChain(db, notifier.Object, tenantId, CancellationToken.None);

        notifier.Verify(
            n => n.Notify(
                It.Is<NotificationRequest>(r => r.EventType == "AuditIntegrityBreach"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Broken_Chain_Linkage_Is_Detected()
    {
        await using var db = CreateDb(nameof(Broken_Chain_Linkage_Is_Detected));
        var tenantId = Guid.NewGuid();
        await SeedValidChain(db, tenantId);

        // Break the chain linkage by modifying PreviousHash
        var entry = await db.AuditLog.Where(e => e.SequenceNumber == 2).SingleAsync();
        entry.PreviousHash = "wrong_previous_hash";
        await db.SaveChangesAsync();

        var notifier = new Mock<INotificationOrchestrator>();
        var sut = CreateJob(db, notifier.Object);

        await sut.VerifyTenantChain(db, notifier.Object, tenantId, CancellationToken.None);

        notifier.Verify(
            n => n.Notify(
                It.Is<NotificationRequest>(r =>
                    r.EventType == "AuditIntegrityBreach" &&
                    r.Priority == Domain.Enums.NotificationPriority.Critical),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunVerification_Processes_All_Tenants()
    {
        await using var db = CreateDb(nameof(RunVerification_Processes_All_Tenants));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedValidChain(db, tenantA);
        await SeedValidChain(db, tenantB);

        var notifier = new Mock<INotificationOrchestrator>();
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<MetadataDbContext>(db);
        services.AddSingleton(notifier.Object);
        var provider = services.BuildServiceProvider();

        var sut = new AuditIntegrityVerificationJob(
            provider,
            NullLogger<AuditIntegrityVerificationJob>.Instance);

        await sut.RunVerification(CancellationToken.None);

        // No alerts should be sent for valid chains
        notifier.Verify(
            n => n.Notify(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task SeedValidChain(MetadataDbContext db, Guid tenantId)
    {
        var timestamp1 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var timestamp2 = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        var hash1 = AuditLogger.ComputeHash(
            1, "Submission", timestamp1, tenantId, "user1",
            "Submission", 1, "Create", null, null, "GENESIS");

        var hash2 = AuditLogger.ComputeHash(
            2, "Submission", timestamp2, tenantId, "user1",
            "Submission", 1, "Update", null, null, hash1);

        db.AuditLog.AddRange(
            new AuditLogEntry
            {
                TenantId = tenantId,
                EntityType = "Submission",
                EntityId = 1,
                Action = "Create",
                PerformedBy = "user1",
                PerformedAt = timestamp1,
                Hash = hash1,
                PreviousHash = "GENESIS",
                SequenceNumber = 1
            },
            new AuditLogEntry
            {
                TenantId = tenantId,
                EntityType = "Submission",
                EntityId = 1,
                Action = "Update",
                PerformedBy = "user1",
                PerformedAt = timestamp2,
                Hash = hash2,
                PreviousHash = hash1,
                SequenceNumber = 2
            });
        await db.SaveChangesAsync();
    }

    private static MetadataDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MetadataDbContext(options);
    }

    private static AuditIntegrityVerificationJob CreateJob(
        MetadataDbContext db,
        INotificationOrchestrator notifier)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<MetadataDbContext>(db);
        services.AddSingleton(notifier);
        var provider = services.BuildServiceProvider();

        return new AuditIntegrityVerificationJob(
            provider,
            NullLogger<AuditIntegrityVerificationJob>.Instance);
    }
}
