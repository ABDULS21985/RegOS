using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class PlatformIntelligenceRefreshRunStoreServiceTests
{
    [Fact]
    public async Task RecordSuccessAndFailureAsync_Persists_And_Loads_Latest_Run_State()
    {
        await using var db = CreateDb();
        var sut = new PlatformIntelligenceRefreshRunStoreService(db);

        var successStartedAt = new DateTime(2026, 3, 12, 8, 0, 0, DateTimeKind.Utc);
        var successCompletedAt = successStartedAt.AddSeconds(18);
        var failureStartedAt = successCompletedAt.AddMinutes(5);
        var failureCompletedAt = failureStartedAt.AddSeconds(4);

        await sut.RecordSuccessAsync(new PlatformIntelligenceRefreshRunSuccessCommand
        {
            StartedAtUtc = successStartedAt,
            CompletedAtUtc = successCompletedAt,
            GeneratedAtUtc = successCompletedAt.AddSeconds(-1),
            DurationMilliseconds = 18000,
            InstitutionCount = 7,
            InterventionCount = 12,
            TimelineCount = 24,
            DashboardPacksMaterialized = 9
        });

        await sut.RecordFailureAsync(new PlatformIntelligenceRefreshRunFailureCommand
        {
            StartedAtUtc = failureStartedAt,
            CompletedAtUtc = failureCompletedAt,
            DurationMilliseconds = 4000,
            FailureMessage = "scheduler timeout"
        });

        var loaded = await sut.LoadLatestAsync();
        var recent = await sut.LoadRecentAsync();

        loaded.Should().NotBeNull();
        loaded!.Succeeded.Should().BeFalse();
        loaded.Status.Should().Be("Failed");
        loaded.CompletedAtUtc.Should().Be(failureCompletedAt);
        loaded.LastSuccessfulCompletedAtUtc.Should().Be(successCompletedAt);
        loaded.LastFailedCompletedAtUtc.Should().Be(failureCompletedAt);
        loaded.FailureMessage.Should().Contain("scheduler timeout");
        recent.Should().HaveCount(2);
        recent[0].CompletedAtUtc.Should().Be(failureCompletedAt);
        recent[0].Succeeded.Should().BeFalse();
        recent[1].CompletedAtUtc.Should().Be(successCompletedAt);
        recent[1].Succeeded.Should().BeTrue();
        (await db.PlatformIntelligenceRefreshRuns.AsNoTracking().CountAsync()).Should().Be(2);
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
