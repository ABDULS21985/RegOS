using System.Reflection;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Persistence;
using FC.Engine.Infrastructure.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Persistence;

public class GenericDataRepositoryMetadataTests
{
    [Fact]
    public async Task DataSource_SourceDetail_Metadata_Is_Persisted()
    {
        var dbName = nameof(DataSource_SourceDetail_Metadata_Is_Persisted);
        var tenantId = Guid.NewGuid();
        var sut = CreateSut(dbName, tenantId);

        await InvokeMetadataUpsert(
            sut,
            "BDC_AML",
            1001,
            "str_count",
            "InterModule",
            "BDC_CBN/BDC_AML/str_count");

        await using var db = CreateDbContext(dbName);
        var stored = await db.SubmissionFieldSources.SingleAsync();
        stored.TenantId.Should().Be(tenantId);
        stored.ReturnCode.Should().Be("BDC_AML");
        stored.SubmissionId.Should().Be(1001);
        stored.FieldName.Should().Be("str_count");
        stored.DataSource.Should().Be("InterModule");
        stored.SourceDetail.Should().Be("BDC_CBN/BDC_AML/str_count");
    }

    [Fact]
    public async Task User_Can_Override_AutoPopulated_Value()
    {
        var dbName = nameof(User_Can_Override_AutoPopulated_Value);
        var tenantId = Guid.NewGuid();
        var sut = CreateSut(dbName, tenantId);

        await InvokeMetadataUpsert(
            sut,
            "NFIU_STR",
            2002,
            "str_filed_count",
            "InterModule",
            "BDC_CBN/BDC_AML/str_count");

        await InvokeMetadataUpsert(
            sut,
            "NFIU_STR",
            2002,
            "str_filed_count",
            "Manual",
            null);

        await using var db = CreateDbContext(dbName);
        var stored = await db.SubmissionFieldSources.SingleAsync();
        stored.DataSource.Should().Be("Manual");
        stored.SourceDetail.Should().BeNull();
    }

    private static GenericDataRepository CreateSut(string databaseName, Guid tenantId)
    {
        var connectionFactory = new Mock<IDbConnectionFactory>();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(t => t.CurrentTenantId).Returns(tenantId);
        tenantContext.SetupGet(t => t.IsPlatformAdmin).Returns(false);
        tenantContext.SetupGet(t => t.ImpersonatingTenantId).Returns((Guid?)null);

        var cache = new Mock<ITemplateMetadataCache>();
        var sqlBuilder = new DynamicSqlBuilder();

        return new GenericDataRepository(
            connectionFactory.Object,
            tenantContext.Object,
            cache.Object,
            sqlBuilder,
            new TestDbContextFactory(databaseName));
    }

    private static MetadataDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new MetadataDbContext(options);
    }

    private static async Task InvokeMetadataUpsert(
        GenericDataRepository repository,
        string returnCode,
        int submissionId,
        string fieldName,
        string dataSource,
        string? sourceDetail)
    {
        var method = typeof(GenericDataRepository).GetMethod(
            "UpsertFieldSourceMetadata",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var task = method!.Invoke(
            repository,
            new object?[]
            {
                returnCode,
                submissionId,
                fieldName,
                dataSource,
                sourceDetail,
                CancellationToken.None
            }) as Task;

        task.Should().NotBeNull();
        await task!;
    }
}
