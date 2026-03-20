using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Tests.Services;

public class ResilienceAssessmentStoreServiceTests
{
    [Fact]
    public async Task RecordResponseAsync_Persists_And_Removes_Assessment_Answers()
    {
        await using var db = CreateDb();
        var sut = new ResilienceAssessmentStoreService(db);

        await sut.RecordResponseAsync(new ResilienceAssessmentResponseCommand
        {
            QuestionId = "service-inventory",
            Domain = "Critical Service Mapping",
            Prompt = "Important business services are inventoried.",
            Score = 4,
            AnsweredAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });

        await sut.RecordResponseAsync(new ResilienceAssessmentResponseCommand
        {
            QuestionId = "service-inventory",
            Domain = "Critical Service Mapping",
            Prompt = "Important business services are inventoried.",
            Score = 5,
            AnsweredAtUtc = DateTime.UtcNow
        });

        var state = await sut.LoadAsync();

        state.Responses.Should().ContainSingle();
        state.Responses[0].Score.Should().Be(5);

        var persisted = await db.ResilienceAssessmentResponses.AsNoTracking().ToListAsync();
        persisted.Should().ContainSingle();
        persisted[0].Score.Should().Be(5);

        await sut.RecordResponseAsync(new ResilienceAssessmentResponseCommand
        {
            QuestionId = "service-inventory",
            Domain = "Critical Service Mapping",
            Prompt = "Important business services are inventoried.",
            Score = 0,
            AnsweredAtUtc = DateTime.UtcNow
        });

        (await sut.LoadAsync()).Responses.Should().BeEmpty();
        (await db.ResilienceAssessmentResponses.AsNoTracking().ToListAsync()).Should().BeEmpty();
    }

    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetadataDbContext(options);
    }
}
