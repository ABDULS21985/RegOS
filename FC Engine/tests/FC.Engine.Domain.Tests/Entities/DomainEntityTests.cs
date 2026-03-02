using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Validation;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Domain.Tests.Entities;

public class SubmissionTests
{
    [Fact]
    public void Create_ShouldSetStatusToDraftAndSetTimestamps()
    {
        var before = DateTime.UtcNow;

        var submission = Submission.Create(
            institutionId: 10,
            returnPeriodId: 5,
            returnCode: "MFCR 300");

        var after = DateTime.UtcNow;

        submission.InstitutionId.Should().Be(10);
        submission.ReturnPeriodId.Should().Be(5);
        submission.ReturnCode.Should().Be("MFCR 300");
        submission.Status.Should().Be(SubmissionStatus.Draft);
        submission.SubmittedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        submission.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        submission.TemplateVersionId.Should().BeNull();
        submission.RawXml.Should().BeNull();
        submission.ValidationReport.Should().BeNull();
    }

    [Fact]
    public void MarkParsing_ShouldTransitionToParsing()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.MarkParsing();

        submission.Status.Should().Be(SubmissionStatus.Parsing);
    }

    [Fact]
    public void MarkValidating_ShouldTransitionToValidating()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.MarkValidating();

        submission.Status.Should().Be(SubmissionStatus.Validating);
    }

    [Fact]
    public void MarkAccepted_ShouldTransitionToAccepted()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.MarkAccepted();

        submission.Status.Should().Be(SubmissionStatus.Accepted);
    }

    [Fact]
    public void MarkAcceptedWithWarnings_ShouldTransitionToAcceptedWithWarnings()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.MarkAcceptedWithWarnings();

        submission.Status.Should().Be(SubmissionStatus.AcceptedWithWarnings);
    }

    [Fact]
    public void MarkRejected_ShouldTransitionToRejected()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.MarkRejected();

        submission.Status.Should().Be(SubmissionStatus.Rejected);
    }

    [Fact]
    public void SetTemplateVersion_ShouldStoreVersionId()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");

        submission.SetTemplateVersion(42);

        submission.TemplateVersionId.Should().Be(42);
    }

    [Fact]
    public void AttachValidationReport_ShouldStoreReport()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");
        var report = ValidationReport.Create(submission.Id);

        submission.AttachValidationReport(report);

        submission.ValidationReport.Should().BeSameAs(report);
    }

    [Fact]
    public void StoreRawXml_ShouldStoreXml()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");
        var xml = "<Return><Data>test</Data></Return>";

        submission.StoreRawXml(xml);

        submission.RawXml.Should().Be(xml);
    }

    [Fact]
    public void FullLifecycle_CreateThroughAccepted_ShouldTransitionCorrectly()
    {
        var submission = Submission.Create(1, 1, "MFCR 300");
        submission.Status.Should().Be(SubmissionStatus.Draft);

        submission.SetTemplateVersion(10);
        submission.StoreRawXml("<Return />");

        submission.MarkParsing();
        submission.Status.Should().Be(SubmissionStatus.Parsing);

        submission.MarkValidating();
        submission.Status.Should().Be(SubmissionStatus.Validating);

        var report = ValidationReport.Create(submission.Id);
        submission.AttachValidationReport(report);

        submission.MarkAccepted();
        submission.Status.Should().Be(SubmissionStatus.Accepted);

        submission.TemplateVersionId.Should().Be(10);
        submission.RawXml.Should().Be("<Return />");
        submission.ValidationReport.Should().NotBeNull();
    }
}

public class ValidationReportTests
{
    [Fact]
    public void Create_ShouldSetSubmissionIdAndTimestamp()
    {
        var before = DateTime.UtcNow;

        var report = ValidationReport.Create(submissionId: 99);

        var after = DateTime.UtcNow;

        report.SubmissionId.Should().Be(99);
        report.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        report.FinalizedAt.Should().BeNull();
        report.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_WhenNoErrors_ShouldBeTrue()
    {
        var report = ValidationReport.Create(1);

        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenOnlyWarnings_ShouldBeTrue()
    {
        var report = ValidationReport.Create(1);
        report.AddError(CreateValidationError(ValidationSeverity.Warning));

        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenHasErrorSeverityItems_ShouldBeFalse()
    {
        var report = ValidationReport.Create(1);
        report.AddError(CreateValidationError(ValidationSeverity.Error));

        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public void HasWarnings_WhenWarningPresent_ShouldBeTrue()
    {
        var report = ValidationReport.Create(1);
        report.AddError(CreateValidationError(ValidationSeverity.Warning));

        report.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void HasWarnings_WhenNoWarnings_ShouldBeFalse()
    {
        var report = ValidationReport.Create(1);
        report.AddError(CreateValidationError(ValidationSeverity.Error));

        report.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void HasErrors_WhenErrorPresent_ShouldBeTrue()
    {
        var report = ValidationReport.Create(1);
        report.AddError(CreateValidationError(ValidationSeverity.Error));

        report.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void HasErrors_WhenNoErrors_ShouldBeFalse()
    {
        var report = ValidationReport.Create(1);

        report.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void AddError_ShouldAddSingleError()
    {
        var report = ValidationReport.Create(1);
        var error = CreateValidationError(ValidationSeverity.Error, "RULE-001", "total_assets");

        report.AddError(error);

        report.Errors.Should().HaveCount(1);
        report.Errors[0].RuleId.Should().Be("RULE-001");
        report.Errors[0].Field.Should().Be("total_assets");
    }

    [Fact]
    public void AddErrors_ShouldAddMultipleErrors()
    {
        var report = ValidationReport.Create(1);
        var errors = new[]
        {
            CreateValidationError(ValidationSeverity.Error, "RULE-001"),
            CreateValidationError(ValidationSeverity.Warning, "RULE-002"),
            CreateValidationError(ValidationSeverity.Error, "RULE-003")
        };

        report.AddErrors(errors);

        report.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void ErrorCountAndWarningCount_WithMixedSeverities_ShouldReturnCorrectCounts()
    {
        var report = ValidationReport.Create(1);
        report.AddErrors(new[]
        {
            CreateValidationError(ValidationSeverity.Error, "ERR-001"),
            CreateValidationError(ValidationSeverity.Error, "ERR-002"),
            CreateValidationError(ValidationSeverity.Error, "ERR-003"),
            CreateValidationError(ValidationSeverity.Warning, "WARN-001"),
            CreateValidationError(ValidationSeverity.Warning, "WARN-002")
        });

        report.ErrorCount.Should().Be(3);
        report.WarningCount.Should().Be(2);
    }

    [Fact]
    public void FinalizeAt_ShouldSetFinalizationTimestamp()
    {
        var report = ValidationReport.Create(1);
        var finalizeTime = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        report.FinalizeAt(finalizeTime);

        report.FinalizedAt.Should().Be(finalizeTime);
    }

    private static ValidationError CreateValidationError(
        ValidationSeverity severity,
        string ruleId = "RULE-001",
        string field = "test_field")
    {
        return new ValidationError
        {
            RuleId = ruleId,
            Field = field,
            Message = $"Validation failed for {field}",
            Severity = severity,
            Category = ValidationCategory.Schema
        };
    }
}

public class ReturnTemplateTests
{
    [Fact]
    public void CreateDraftVersion_FirstVersion_ShouldGetVersionNumberOne()
    {
        var template = CreateTemplate();

        var version = template.CreateDraftVersion("admin");

        version.VersionNumber.Should().Be(1);
        version.Status.Should().Be(TemplateStatus.Draft);
        version.CreatedBy.Should().Be("admin");
        version.TemplateId.Should().Be(template.Id);
        template.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void CreateDraftVersion_SubsequentVersion_ShouldIncrementVersionNumber()
    {
        var template = CreateTemplate();
        template.CreateDraftVersion("admin");

        var v2 = template.CreateDraftVersion("admin");

        v2.VersionNumber.Should().Be(2);
        template.Versions.Should().HaveCount(2);
    }

    [Fact]
    public void CreateDraftVersion_ThirdVersion_ShouldBeVersionThree()
    {
        var template = CreateTemplate();
        template.CreateDraftVersion("admin");
        template.CreateDraftVersion("admin");

        var v3 = template.CreateDraftVersion("admin");

        v3.VersionNumber.Should().Be(3);
        template.Versions.Should().HaveCount(3);
    }

    [Fact]
    public void CurrentPublishedVersion_WhenNoPublished_ShouldReturnNull()
    {
        var template = CreateTemplate();
        template.CreateDraftVersion("admin");

        template.CurrentPublishedVersion.Should().BeNull();
    }

    [Fact]
    public void CurrentPublishedVersion_WhenPublishedExists_ShouldReturnCorrectVersion()
    {
        var template = CreateTemplate();
        var version = template.CreateDraftVersion("admin");
        version.SubmitForReview();
        version.Publish(DateTime.UtcNow, "admin");

        template.CurrentPublishedVersion.Should().BeSameAs(version);
    }

    [Fact]
    public void CurrentPublishedVersion_WithMultipleVersions_ShouldReturnPublishedOne()
    {
        var template = CreateTemplate();

        var v1 = template.CreateDraftVersion("admin");
        v1.SubmitForReview();
        v1.Publish(DateTime.UtcNow, "admin");

        // v2 is still a draft
        template.CreateDraftVersion("admin");

        template.CurrentPublishedVersion.Should().BeSameAs(v1);
    }

    [Fact]
    public void GetVersion_WhenVersionExists_ShouldReturnIt()
    {
        var template = CreateTemplate();
        var version = template.CreateDraftVersion("admin");
        version.Id = 42;

        var found = template.GetVersion(42);

        found.Should().BeSameAs(version);
    }

    [Fact]
    public void GetVersion_WhenNotFound_ShouldThrow()
    {
        var template = CreateTemplate();
        template.CreateDraftVersion("admin");

        var act = () => template.GetVersion(999);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetPreviousPublishedVersion_WhenNoPrevious_ShouldReturnNull()
    {
        var template = CreateTemplate();
        var v1 = template.CreateDraftVersion("admin");
        v1.Id = 1;
        v1.SubmitForReview();
        v1.Publish(DateTime.UtcNow, "admin");

        template.GetPreviousPublishedVersion(v1.Id).Should().BeNull();
    }

    [Fact]
    public void GetPreviousPublishedVersion_WhenPreviousExists_ShouldReturnIt()
    {
        var template = CreateTemplate();

        var v1 = template.CreateDraftVersion("admin");
        v1.Id = 1;
        v1.SubmitForReview();
        v1.Publish(DateTime.UtcNow, "admin");
        v1.Deprecate();

        var v2 = template.CreateDraftVersion("admin");
        v2.Id = 2;
        v2.SubmitForReview();
        v2.Publish(DateTime.UtcNow, "admin");

        var previous = template.GetPreviousPublishedVersion(v2.Id);

        previous.Should().BeSameAs(v1);
    }

    [Fact]
    public void AddVersion_ShouldAppendToVersionsList()
    {
        var template = CreateTemplate();
        var version = new TemplateVersion
        {
            Id = 1,
            TemplateId = template.Id,
            VersionNumber = 1,
            Status = TemplateStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };

        template.AddVersion(version);

        template.Versions.Should().HaveCount(1);
        template.Versions[0].Should().BeSameAs(version);
    }

    private static ReturnTemplate CreateTemplate()
    {
        return new ReturnTemplate
        {
            Id = 1,
            ReturnCode = "MFCR 300",
            Name = "MFCR 300",
            StructuralCategory = StructuralCategory.FixedRow,
            PhysicalTableName = "mfcr_300"
        };
    }
}

public class CrossSheetRuleTests
{
    [Fact]
    public void AddOperand_ShouldAddOperandToList()
    {
        var rule = CreateRule();
        var operand = new CrossSheetRuleOperand
        {
            OperandAlias = "A",
            TemplateReturnCode = "MFCR 300",
            FieldName = "total_assets",
            SortOrder = 1
        };

        rule.AddOperand(operand);

        rule.Operands.Should().HaveCount(1);
        rule.Operands[0].OperandAlias.Should().Be("A");
        rule.Operands[0].TemplateReturnCode.Should().Be("MFCR 300");
        rule.Operands[0].FieldName.Should().Be("total_assets");
    }

    [Fact]
    public void AddOperand_ShouldSetRuleIdOnOperand()
    {
        var rule = CreateRule();
        rule.Id = 55;
        var operand = new CrossSheetRuleOperand
        {
            OperandAlias = "A",
            TemplateReturnCode = "MFCR 300",
            FieldName = "total_assets",
            SortOrder = 1
        };

        rule.AddOperand(operand);

        operand.RuleId.Should().Be(55);
    }

    [Fact]
    public void AddOperand_MultipleTimes_ShouldAccumulateOperands()
    {
        var rule = CreateRule();

        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "A",
            TemplateReturnCode = "MFCR 300",
            FieldName = "total_assets",
            SortOrder = 1
        });

        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "B",
            TemplateReturnCode = "MFCR 400",
            FieldName = "total_liabilities",
            SortOrder = 2
        });

        rule.Operands.Should().HaveCount(2);
        rule.Operands[0].OperandAlias.Should().Be("A");
        rule.Operands[1].OperandAlias.Should().Be("B");
    }

    [Fact]
    public void SetOperands_ShouldReplaceAllOperands()
    {
        var rule = CreateRule();
        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "OLD",
            TemplateReturnCode = "MFCR 100",
            FieldName = "old_field",
            SortOrder = 1
        });

        var newOperands = new[]
        {
            new CrossSheetRuleOperand
            {
                OperandAlias = "X",
                TemplateReturnCode = "MFCR 300",
                FieldName = "field_x",
                SortOrder = 1
            },
            new CrossSheetRuleOperand
            {
                OperandAlias = "Y",
                TemplateReturnCode = "MFCR 400",
                FieldName = "field_y",
                SortOrder = 2
            }
        };

        rule.SetOperands(newOperands);

        rule.Operands.Should().HaveCount(2);
        rule.Operands.Select(o => o.OperandAlias).Should().Contain("X", "Y");
        rule.Operands.Select(o => o.OperandAlias).Should().NotContain("OLD");
    }

    [Fact]
    public void SetOperands_ShouldClearExistingBeforeAdding()
    {
        var rule = CreateRule();

        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "A",
            TemplateReturnCode = "MFCR 300",
            FieldName = "field_a",
            SortOrder = 1
        });
        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "B",
            TemplateReturnCode = "MFCR 400",
            FieldName = "field_b",
            SortOrder = 2
        });

        rule.Operands.Should().HaveCount(2);

        rule.SetOperands(new[]
        {
            new CrossSheetRuleOperand
            {
                OperandAlias = "Z",
                TemplateReturnCode = "MFCR 500",
                FieldName = "field_z",
                SortOrder = 1
            }
        });

        rule.Operands.Should().HaveCount(1);
        rule.Operands[0].OperandAlias.Should().Be("Z");
    }

    [Fact]
    public void SetOperands_WithEmptyCollection_ShouldClearAll()
    {
        var rule = CreateRule();
        rule.AddOperand(new CrossSheetRuleOperand
        {
            OperandAlias = "A",
            TemplateReturnCode = "MFCR 300",
            FieldName = "field_a",
            SortOrder = 1
        });

        rule.SetOperands(Array.Empty<CrossSheetRuleOperand>());

        rule.Operands.Should().BeEmpty();
    }

    [Fact]
    public void NewRule_ShouldHaveEmptyOperands()
    {
        var rule = CreateRule();

        rule.Operands.Should().BeEmpty();
    }

    [Fact]
    public void Expression_ShouldBeSettable()
    {
        var rule = CreateRule();
        var expression = new CrossSheetRuleExpression
        {
            RuleId = rule.Id,
            Expression = "A = B",
            ToleranceAmount = 0.01m,
            ErrorMessage = "Values must match"
        };

        rule.Expression = expression;

        rule.Expression.Should().NotBeNull();
        rule.Expression!.Expression.Should().Be("A = B");
        rule.Expression.ToleranceAmount.Should().Be(0.01m);
    }

    private static CrossSheetRule CreateRule()
    {
        return new CrossSheetRule
        {
            Id = 1,
            RuleCode = "XS-001",
            RuleName = "Asset Liability Cross Check",
            Description = "Validates assets equal liabilities plus equity",
            Severity = ValidationSeverity.Error,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }
}
