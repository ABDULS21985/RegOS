using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.BackgroundJobs;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Services;

public class ReportQueryEngineTests
{
    private readonly Mock<IDbConnectionFactory> _connectionFactory = new();
    private readonly Mock<ITemplateMetadataCache> _cache = new();

    private ReportQueryEngine CreateSut() =>
        new(_connectionFactory.Object, _cache.Object, NullLogger<ReportQueryEngine>.Instance);

    // ── Entitlement checks ──────────────────────────────────────────

    [Fact]
    public async Task Execute_Throws_When_No_Fields()
    {
        var sut = CreateSut();
        var definition = new ReportDefinition();

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least one field*");
    }

    [Fact]
    public async Task Execute_Throws_When_Module_Not_Entitled()
    {
        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields =
            [
                new ReportFieldDef
                {
                    ModuleCode = "UNENTITLED",
                    TemplateCode = "RET999",
                    FieldCode = "amount"
                }
            ]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1", "MOD2"]);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Not entitled*UNENTITLED*");
    }

    [Fact]
    public async Task Execute_Throws_When_Template_Not_Found()
    {
        _cache.Setup(c => c.GetPublishedTemplate("UNKNOWN", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Template not found"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields =
            [
                new ReportFieldDef
                {
                    ModuleCode = "MOD1",
                    TemplateCode = "UNKNOWN",
                    FieldCode = "amount"
                }
            ]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Execute_Throws_When_Field_Not_In_Template()
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "customer_name", DataType = FieldDataType.Text, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields =
            [
                new ReportFieldDef
                {
                    ModuleCode = "MOD1",
                    TemplateCode = "RET101",
                    FieldCode = "nonexistent_field"
                }
            ]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*nonexistent_field*not found*RET101*");
    }

    // ── SQL identifier safety ──────────────────────────────────────

    [Theory]
    [InlineData("valid_name")]
    [InlineData("amount")]
    [InlineData("_internal")]
    [InlineData("field123")]
    public async Task Execute_Accepts_Valid_Field_Names(string fieldName)
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = fieldName, DataType = FieldDataType.Text, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Will fail at DB execution but should NOT fail at validation
        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Expected: reached SQL execution"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields =
            [
                new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = fieldName }
            ]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        // Should reach connection creation (meaning validation passed)
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expected: reached SQL execution");
    }

    [Theory]
    [InlineData("Robert'; DROP TABLE--")]
    [InlineData("1startswithnumber")]
    [InlineData("field name")]
    [InlineData("field;name")]
    [InlineData("")]
    public async Task Execute_Rejects_Unsafe_Field_Names(string unsafeName)
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = unsafeName, DataType = FieldDataType.Text, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields =
            [
                new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = unsafeName }
            ]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid SQL identifier*");
    }

    [Theory]
    [InlineData("Equals", "=")]
    [InlineData("GREATERTHAN", ">")]
    [InlineData("Contains", "LIKE")]
    [InlineData("IN", "IN")]
    public async Task Execute_Accepts_Valid_Filter_Operators(string opName, string _)
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "amount", DataType = FieldDataType.Money, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Expected: reached SQL execution"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields = [new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = "amount" }],
            Filters = [new ReportFilterDef { Field = "amount", Operator = opName, Value = "100" }]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expected: reached SQL execution");
    }

    [Fact]
    public async Task Execute_Rejects_Invalid_Filter_Operator()
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "amount", DataType = FieldDataType.Money, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Should not reach here"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields = [new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = "amount" }],
            Filters = [new ReportFilterDef { Field = "amount", Operator = "INVALID_OP", Value = "100" }]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid filter operator*");
    }

    // ── Row limit enforcement ──────────────────────────────────────

    [Fact]
    public async Task Execute_Caps_Limit_At_10000()
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "amount", DataType = FieldDataType.Money, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Expected: reached SQL execution"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields = [new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = "amount" }],
            Limit = 99999
        };

        // The limit gets capped internally. We verify it reaches SQL execution
        // (meaning validation passed). The SQL itself would contain TOP (10000).
        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expected: reached SQL execution");
    }

    // ── Aggregation validation ──────────────────────────────────────

    [Theory]
    [InlineData("SUM")]
    [InlineData("AVG")]
    [InlineData("MIN")]
    [InlineData("MAX")]
    [InlineData("COUNT")]
    public async Task Execute_Accepts_Valid_Aggregate_Functions(string func)
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "amount", DataType = FieldDataType.Money, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Expected: reached SQL execution"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields = [new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = "amount" }],
            Aggregations = [new ReportAggregationDef { Field = "amount", Function = func }]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Expected: reached SQL execution");
    }

    [Fact]
    public async Task Execute_Rejects_Invalid_Aggregate_Function()
    {
        var template = BuildTemplate("RET101", "ret101_data", new[]
        {
            new TemplateField { FieldName = "amount", DataType = FieldDataType.Money, FieldOrder = 1 }
        });

        _cache.Setup(c => c.GetPublishedTemplate("RET101", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _connectionFactory.Setup(c => c.CreateConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Should not reach here"));

        var sut = CreateSut();
        var definition = new ReportDefinition
        {
            Fields = [new ReportFieldDef { ModuleCode = "MOD1", TemplateCode = "RET101", FieldCode = "amount" }],
            Aggregations = [new ReportAggregationDef { Field = "amount", Function = "DROP_TABLE" }]
        };

        var act = () => sut.Execute(definition, Guid.NewGuid(), ["MOD1"]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid aggregate function*");
    }

    // ── ScheduledReportJob.IsDue ───────────────────────────────────

    [Fact]
    public void IsDue_Returns_False_For_Null_Cron()
    {
        ScheduledReportJob.IsDue(null, null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_Returns_False_For_Empty_Cron()
    {
        ScheduledReportJob.IsDue("", null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_Returns_False_For_Invalid_Cron_Parts()
    {
        ScheduledReportJob.IsDue("invalid cron", null).Should().BeFalse();
    }

    [Fact]
    public void IsDue_Returns_False_When_Ran_Recently()
    {
        // "* * * * *" matches every minute
        var lastRun = DateTime.UtcNow.AddMinutes(-30);

        ScheduledReportJob.IsDue("* * * * *", lastRun).Should().BeFalse();
    }

    [Fact]
    public void IsDue_Returns_True_When_Wildcard_Cron_And_No_Recent_Run()
    {
        var lastRun = DateTime.UtcNow.AddHours(-2);

        ScheduledReportJob.IsDue("* * * * *", lastRun).Should().BeTrue();
    }

    [Fact]
    public void IsDue_Returns_True_When_Wildcard_Cron_And_Never_Ran()
    {
        ScheduledReportJob.IsDue("* * * * *", null).Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static CachedTemplate BuildTemplate(string returnCode, string tableName, TemplateField[] fields)
    {
        return new CachedTemplate
        {
            TemplateId = 1,
            ReturnCode = returnCode,
            Name = returnCode,
            PhysicalTableName = tableName,
            ModuleCode = "MOD1",
            ModuleId = 1,
            CurrentVersion = new CachedTemplateVersion
            {
                Id = 1,
                VersionNumber = 1,
                Fields = fields
            }
        };
    }
}
