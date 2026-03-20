using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.DataRecord;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Validation;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Validation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FC.Engine.Infrastructure.Tests.Validation;

public class CrossSheetValidatorCrossModuleTests
{
    [Fact]
    public async Task CrossModule_Rule_Fires_When_Both_Modules_Active()
    {
        await using var db = CreateDbContext(nameof(CrossModule_Rule_Fires_When_Both_Modules_Active));
        var tenantId = Guid.NewGuid();

        var sourceModule = new Module
        {
            ModuleCode = "BDC_CBN",
            ModuleName = "BDC",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        var targetModule = new Module
        {
            ModuleCode = "NFIU_AML",
            ModuleName = "NFIU",
            RegulatorCode = "NFIU",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.AddRange(sourceModule, targetModule);
        await db.SaveChangesAsync();

        var sourceSubmission = Submission.Create(100, 202601, "BDC_AML", tenantId);
        var targetSubmission = Submission.Create(100, 202601, "NFIU_STR", tenantId);
        db.Submissions.AddRange(sourceSubmission, targetSubmission);
        db.CrossSheetRules.Add(new CrossSheetRule
        {
            RuleCode = "CM-001",
            RuleName = "Cross module reconciliation",
            Description = "STR counts should reconcile",
            ModuleId = sourceModule.Id,
            SourceModuleId = sourceModule.Id,
            TargetModuleId = targetModule.Id,
            SourceTemplateCode = "BDC_AML",
            SourceFieldCode = "str_count",
            TargetTemplateCode = "NFIU_STR",
            TargetFieldCode = "str_filed_count",
            Operator = "Equals",
            ToleranceAmount = 0m,
            Severity = ValidationSeverity.Warning,
            IsActive = true,
            CreatedBy = "tester",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var formulaRepo = new Mock<IFormulaRepository>();
        var cache = new Mock<ITemplateMetadataCache>();
        var entitlement = new Mock<IEntitlementService>();
        entitlement.Setup(e => e.HasModuleAccess(tenantId, "NFIU_AML", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dataRepo = new Mock<IGenericDataRepository>();
        dataRepo.Setup(d => d.GetBySubmission("BDC_AML", sourceSubmission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecord("BDC_AML", ("str_count", 12m)));
        dataRepo.Setup(d => d.GetBySubmission("NFIU_STR", targetSubmission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRecord("NFIU_STR", ("str_filed_count", 8m)));

        var sut = new CrossSheetValidator(
            formulaRepo.Object,
            dataRepo.Object,
            cache.Object,
            db,
            entitlement.Object);

        var errors = await sut.ValidateCrossModule(
            tenantId,
            sourceSubmission.Id,
            "BDC_CBN",
            100,
            202601);

        errors.Should().ContainSingle();
        errors[0].RuleId.Should().Be("CM-001");
        errors[0].Field.Should().Be("str_count");
    }

    [Fact]
    public async Task CrossModule_Rule_Skipped_When_Target_Module_Inactive()
    {
        await using var db = CreateDbContext(nameof(CrossModule_Rule_Skipped_When_Target_Module_Inactive));
        var tenantId = Guid.NewGuid();

        var sourceModule = new Module
        {
            ModuleCode = "BDC_CBN",
            ModuleName = "BDC",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        var targetModule = new Module
        {
            ModuleCode = "NFIU_AML",
            ModuleName = "NFIU",
            RegulatorCode = "NFIU",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.AddRange(sourceModule, targetModule);
        await db.SaveChangesAsync();

        var sourceSubmission = Submission.Create(100, 202601, "BDC_AML", tenantId);
        var targetSubmission = Submission.Create(100, 202601, "NFIU_STR", tenantId);
        db.Submissions.AddRange(sourceSubmission, targetSubmission);
        db.CrossSheetRules.Add(new CrossSheetRule
        {
            RuleCode = "CM-002",
            RuleName = "Cross module reconciliation",
            ModuleId = sourceModule.Id,
            SourceModuleId = sourceModule.Id,
            TargetModuleId = targetModule.Id,
            SourceTemplateCode = "BDC_AML",
            SourceFieldCode = "str_count",
            TargetTemplateCode = "NFIU_STR",
            TargetFieldCode = "str_filed_count",
            Operator = "Equals",
            ToleranceAmount = 0m,
            Severity = ValidationSeverity.Warning,
            IsActive = true,
            CreatedBy = "tester",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var formulaRepo = new Mock<IFormulaRepository>();
        var cache = new Mock<ITemplateMetadataCache>();
        var entitlement = new Mock<IEntitlementService>();
        entitlement.Setup(e => e.HasModuleAccess(tenantId, "NFIU_AML", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var dataRepo = new Mock<IGenericDataRepository>();
        var sut = new CrossSheetValidator(
            formulaRepo.Object,
            dataRepo.Object,
            cache.Object,
            db,
            entitlement.Object);

        var errors = await sut.ValidateCrossModule(
            tenantId,
            sourceSubmission.Id,
            "BDC_CBN",
            100,
            202601);

        errors.Should().BeEmpty();
        dataRepo.Verify(
            d => d.GetBySubmission(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CrossModule_Rule_Skipped_When_No_Target_Data_For_Period()
    {
        await using var db = CreateDbContext(nameof(CrossModule_Rule_Skipped_When_No_Target_Data_For_Period));
        var tenantId = Guid.NewGuid();

        var sourceModule = new Module
        {
            ModuleCode = "BDC_CBN",
            ModuleName = "BDC",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        var targetModule = new Module
        {
            ModuleCode = "NFIU_AML",
            ModuleName = "NFIU",
            RegulatorCode = "NFIU",
            DefaultFrequency = "Monthly",
            CreatedAt = DateTime.UtcNow
        };
        db.Modules.AddRange(sourceModule, targetModule);
        await db.SaveChangesAsync();

        var sourceSubmission = Submission.Create(100, 202601, "BDC_AML", tenantId);
        db.Submissions.Add(sourceSubmission);
        db.CrossSheetRules.Add(new CrossSheetRule
        {
            RuleCode = "CM-003",
            RuleName = "Cross module reconciliation",
            ModuleId = sourceModule.Id,
            SourceModuleId = sourceModule.Id,
            TargetModuleId = targetModule.Id,
            SourceTemplateCode = "BDC_AML",
            SourceFieldCode = "str_count",
            TargetTemplateCode = "NFIU_STR",
            TargetFieldCode = "str_filed_count",
            Operator = "Equals",
            ToleranceAmount = 0m,
            Severity = ValidationSeverity.Warning,
            IsActive = true,
            CreatedBy = "tester",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var formulaRepo = new Mock<IFormulaRepository>();
        var cache = new Mock<ITemplateMetadataCache>();
        var entitlement = new Mock<IEntitlementService>();
        entitlement.Setup(e => e.HasModuleAccess(tenantId, "NFIU_AML", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dataRepo = new Mock<IGenericDataRepository>();
        var sut = new CrossSheetValidator(
            formulaRepo.Object,
            dataRepo.Object,
            cache.Object,
            db,
            entitlement.Object);

        var errors = await sut.ValidateCrossModule(
            tenantId,
            sourceSubmission.Id,
            "BDC_CBN",
            100,
            202601);

        errors.Should().BeEmpty();
        dataRepo.Verify(
            d => d.GetBySubmission("NFIU_STR", It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static MetadataDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new MetadataDbContext(options);
    }

    private static ReturnDataRecord CreateRecord(string returnCode, params (string field, decimal value)[] values)
    {
        var record = new ReturnDataRecord(returnCode, 1, StructuralCategory.FixedRow);
        var row = new ReturnDataRow();
        foreach (var (field, value) in values)
        {
            row.SetValue(field, value);
        }
        record.AddRow(row);
        return record;
    }
}
