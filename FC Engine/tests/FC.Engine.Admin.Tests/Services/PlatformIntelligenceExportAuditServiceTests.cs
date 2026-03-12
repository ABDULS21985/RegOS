using System.Text.Json;
using FC.Engine.Admin.Services;
using FC.Engine.Infrastructure.Metadata;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FC.Engine.Admin.Tests.Services;

public class PlatformIntelligenceExportAuditServiceTests
{
    [Fact]
    public async Task GetRecentExportsAsync_Parses_And_Orders_Recent_Export_Audit_Rows()
    {
        await using var db = CreateDbContext();
        db.AuditLog.AddRange(
            new AuditLogEntry
            {
                EntityType = "PlatformIntelligence",
                EntityId = 0,
                Action = "OverviewExported",
                NewValues = JsonSerializer.Serialize(new
                {
                    Area = "Overview",
                    Format = "csv",
                    FileName = "platform-intelligence-overview.csv",
                    SizeBytes = 512
                }),
                PerformedBy = "analyst-1",
                PerformedAt = new DateTime(2026, 3, 12, 7, 45, 0, DateTimeKind.Utc),
                Hash = "hash-1",
                PreviousHash = "GENESIS",
                SequenceNumber = 1
            },
            new AuditLogEntry
            {
                EntityType = "PlatformIntelligence",
                EntityId = 0,
                Action = "BundleExported",
                NewValues = JsonSerializer.Serialize(new
                {
                    Lens = "executive",
                    InstitutionId = 44,
                    FileName = "platform-intelligence-bundle-executive-44.zip",
                    SizeBytes = 4096
                }),
                PerformedBy = "analyst-2",
                PerformedAt = new DateTime(2026, 3, 12, 8, 0, 0, DateTimeKind.Utc),
                Hash = "hash-2",
                PreviousHash = "hash-1",
                SequenceNumber = 2
            },
            new AuditLogEntry
            {
                EntityType = "PlatformIntelligence",
                EntityId = 0,
                Action = "RefreshTriggered",
                NewValues = JsonSerializer.Serialize(new { Area = "Refresh" }),
                PerformedBy = "system",
                PerformedAt = new DateTime(2026, 3, 12, 8, 5, 0, DateTimeKind.Utc),
                Hash = "hash-3",
                PreviousHash = "hash-2",
                SequenceNumber = 3
            });
        await db.SaveChangesAsync();

        var sut = new PlatformIntelligenceExportAuditService(db);

        var rows = await sut.GetRecentExportsAsync(area: null, format: null, action: null, take: 10);

        rows.Should().HaveCount(2);
        rows[0].Action.Should().Be("BundleExported");
        rows[0].Area.Should().Be("Bundle");
        rows[0].Format.Should().Be("zip");
        rows[0].Lens.Should().Be("executive");
        rows[0].InstitutionId.Should().Be(44);
        rows[0].SizeBytes.Should().Be(4096);
        rows[1].Action.Should().Be("OverviewExported");
        rows[1].Area.Should().Be("Overview");
        rows[1].Format.Should().Be("csv");
    }

    [Fact]
    public async Task GetRecentExportsAsync_Filters_By_Area_Format_And_Action()
    {
        await using var db = CreateDbContext();
        db.AuditLog.AddRange(
            CreateAuditEntry(
                "DashboardBriefingPackExported",
                new { Area = "Dashboards", Format = "pdf", FileName = "briefing-pack.pdf" },
                new DateTime(2026, 3, 12, 8, 10, 0, DateTimeKind.Utc)),
            CreateAuditEntry(
                "DashboardBriefingPackExported",
                new { Area = "Dashboards", Format = "csv", FileName = "briefing-pack.csv" },
                new DateTime(2026, 3, 12, 8, 5, 0, DateTimeKind.Utc)),
            CreateAuditEntry(
                "CapitalPackExported",
                new { Area = "Capital", Format = "csv", FileName = "capital-pack.csv" },
                new DateTime(2026, 3, 12, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var sut = new PlatformIntelligenceExportAuditService(db);

        var rows = await sut.GetRecentExportsAsync("Dashboards", "pdf", "DashboardBriefingPackExported", take: 10);

        rows.Should().ContainSingle();
        rows[0].FileName.Should().Be("briefing-pack.pdf");
        rows[0].Area.Should().Be("Dashboards");
        rows[0].Format.Should().Be("pdf");
    }

    private static AuditLogEntry CreateAuditEntry(string action, object payload, DateTime performedAt) =>
        new()
        {
            EntityType = "PlatformIntelligence",
            EntityId = 0,
            Action = action,
            NewValues = JsonSerializer.Serialize(payload),
            PerformedBy = "platform-admin",
            PerformedAt = performedAt,
            Hash = Guid.NewGuid().ToString("N"),
            PreviousHash = "GENESIS",
            SequenceNumber = performedAt.Ticks
        };

    private static MetadataDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
