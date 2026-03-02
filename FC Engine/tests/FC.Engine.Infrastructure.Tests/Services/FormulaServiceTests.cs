using FC.Engine.Application.DTOs;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Validation;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class FormulaServiceTests
{
    private readonly Mock<IFormulaRepository> _formulaRepo = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IAuditLogger> _audit = new();

    private FormulaService CreateService() =>
        new(_formulaRepo.Object, _templateRepo.Object, _audit.Object);

    // ──────────────────────────────────────────────────────────────────
    // 1. GetIntraSheetFormulas — returns mapped DTOs
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIntraSheetFormulas_ReturnsMappedDtos()
    {
        // Arrange
        var versionId = 10;
        var formulas = new List<IntraSheetFormula>
        {
            new()
            {
                Id = 1,
                TemplateVersionId = versionId,
                RuleCode = "ISF-001",
                RuleName = "Total Cash",
                FormulaType = FormulaType.Sum,
                TargetFieldName = "total_cash",
                TargetLineCode = "10140",
                OperandFields = "[\"cash_notes\",\"cash_coins\"]",
                CustomExpression = null,
                ToleranceAmount = 0.01m,
                TolerancePercent = 0.5m,
                Severity = ValidationSeverity.Error,
                IsActive = true,
                SortOrder = 1
            },
            new()
            {
                Id = 2,
                TemplateVersionId = versionId,
                RuleCode = "ISF-002",
                RuleName = "Net Loans",
                FormulaType = FormulaType.Difference,
                TargetFieldName = "net_loans",
                TargetLineCode = "11930",
                OperandFields = "[\"gross_loans\",\"impairment_loans\"]",
                CustomExpression = null,
                ToleranceAmount = 0m,
                TolerancePercent = null,
                Severity = ValidationSeverity.Warning,
                IsActive = true,
                SortOrder = 2
            }
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulas(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(formulas);

        var service = CreateService();

        // Act
        var result = await service.GetIntraSheetFormulas(versionId);

        // Assert
        result.Should().HaveCount(2);

        var first = result[0];
        first.Id.Should().Be(1);
        first.RuleCode.Should().Be("ISF-001");
        first.RuleName.Should().Be("Total Cash");
        first.FormulaType.Should().Be("Sum");
        first.TargetFieldName.Should().Be("total_cash");
        first.TargetLineCode.Should().Be("10140");
        first.OperandFields.Should().Be("[\"cash_notes\",\"cash_coins\"]");
        first.CustomExpression.Should().BeNull();
        first.ToleranceAmount.Should().Be(0.01m);
        first.TolerancePercent.Should().Be(0.5m);
        first.Severity.Should().Be("Error");
        first.IsActive.Should().BeTrue();

        var second = result[1];
        second.Id.Should().Be(2);
        second.RuleCode.Should().Be("ISF-002");
        second.RuleName.Should().Be("Net Loans");
        second.FormulaType.Should().Be("Difference");
        second.Severity.Should().Be("Warning");
        second.TolerancePercent.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    // 2. GetIntraSheetFormulas — empty list when no formulas
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIntraSheetFormulas_WhenNoFormulas_ReturnsEmptyList()
    {
        // Arrange
        var versionId = 99;
        _formulaRepo.Setup(r => r.GetIntraSheetFormulas(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntraSheetFormula>());

        var service = CreateService();

        // Act
        var result = await service.GetIntraSheetFormulas(versionId);

        // Assert
        result.Should().BeEmpty();
        _formulaRepo.Verify(r => r.GetIntraSheetFormulas(versionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // 3. AddIntraSheetFormula — success with Draft version
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddIntraSheetFormula_WithDraftVersion_CreatesFormulaAndAudits()
    {
        // Arrange
        var templateId = 1;
        var versionId = 5;

        var version = new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };

        var template = new ReturnTemplate
        {
            Id = templateId,
            ReturnCode = "MFCR 300",
            Name = "Statement of Financial Position"
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetById(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _formulaRepo.Setup(r => r.AddIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddIntraSheetFormula(
            templateId, versionId,
            "ISF-010", "Total Assets", FormulaType.Sum,
            "total_assets", "[\"cash\",\"investments\"]",
            null, 0.01m,
            ValidationSeverity.Error, "testuser");

        // Assert
        _formulaRepo.Verify(r => r.AddIntraSheetFormula(
            It.Is<IntraSheetFormula>(f =>
                f.TemplateVersionId == versionId &&
                f.RuleCode == "ISF-010" &&
                f.RuleName == "Total Assets" &&
                f.FormulaType == FormulaType.Sum &&
                f.TargetFieldName == "total_assets" &&
                f.OperandFields == "[\"cash\",\"investments\"]" &&
                f.CustomExpression == null &&
                f.ToleranceAmount == 0.01m &&
                f.Severity == ValidationSeverity.Error &&
                f.IsActive == true &&
                f.CreatedBy == "testuser"),
            It.IsAny<CancellationToken>()), Times.Once);

        _audit.Verify(a => a.Log(
            "IntraSheetFormula", It.IsAny<int>(), "Created",
            null, It.IsAny<IntraSheetFormula>(), "testuser",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // 4. AddIntraSheetFormula — throws when version not Draft
    // ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TemplateStatus.Review)]
    [InlineData(TemplateStatus.Published)]
    [InlineData(TemplateStatus.Deprecated)]
    public async Task AddIntraSheetFormula_WhenVersionNotDraft_ThrowsInvalidOperationException(TemplateStatus status)
    {
        // Arrange
        var templateId = 1;
        var versionId = 5;

        var version = new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };

        var template = new ReturnTemplate
        {
            Id = templateId,
            ReturnCode = "MFCR 300",
            Name = "SFP"
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetById(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var service = CreateService();

        // Act
        var act = () => service.AddIntraSheetFormula(
            templateId, versionId,
            "ISF-010", "Total", FormulaType.Sum,
            "total", "[]", null, 0m,
            ValidationSeverity.Error, "testuser");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft*");

        _formulaRepo.Verify(r => r.AddIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.Log(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // 5. AddIntraSheetFormula — throws when template not found
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddIntraSheetFormula_WhenTemplateNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var templateId = 999;
        _templateRepo.Setup(r => r.GetById(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReturnTemplate?)null);

        var service = CreateService();

        // Act
        var act = () => service.AddIntraSheetFormula(
            templateId, 1,
            "ISF-010", "Total", FormulaType.Sum,
            "total", "[]", null, 0m,
            ValidationSeverity.Error, "testuser");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Template {templateId}*not found*");

        _formulaRepo.Verify(r => r.AddIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // 6. AddCrossSheetRule — creates rule with operands
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddCrossSheetRule_CreatesRuleWithOperandsAndAudits()
    {
        // Arrange
        var operands = new List<CrossSheetRuleOperand>
        {
            new()
            {
                OperandAlias = "A",
                TemplateReturnCode = "MFCR 300",
                FieldName = "total_assets",
                SortOrder = 1
            },
            new()
            {
                OperandAlias = "B",
                TemplateReturnCode = "MFCR 310",
                FieldName = "total_liabilities",
                SortOrder = 2
            }
        };

        var expression = new CrossSheetRuleExpression
        {
            Expression = "A = B",
            ToleranceAmount = 0.50m
        };

        CrossSheetRule? capturedRule = null;
        _formulaRepo.Setup(r => r.AddCrossSheetRule(It.IsAny<CrossSheetRule>(), It.IsAny<CancellationToken>()))
            .Callback<CrossSheetRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddCrossSheetRule(
            "CSR-001", "Assets Equal Liabilities", "Cross-check between sheets",
            operands, expression,
            ValidationSeverity.Error, "admin");

        // Assert
        capturedRule.Should().NotBeNull();
        capturedRule!.RuleCode.Should().Be("CSR-001");
        capturedRule.RuleName.Should().Be("Assets Equal Liabilities");
        capturedRule.Description.Should().Be("Cross-check between sheets");
        capturedRule.Severity.Should().Be(ValidationSeverity.Error);
        capturedRule.IsActive.Should().BeTrue();
        capturedRule.CreatedBy.Should().Be("admin");
        capturedRule.Expression.Should().NotBeNull();
        capturedRule.Expression!.Expression.Should().Be("A = B");
        capturedRule.Expression.ToleranceAmount.Should().Be(0.50m);
        capturedRule.Operands.Should().HaveCount(2);
        capturedRule.Operands[0].OperandAlias.Should().Be("A");
        capturedRule.Operands[0].TemplateReturnCode.Should().Be("MFCR 300");
        capturedRule.Operands[1].OperandAlias.Should().Be("B");
        capturedRule.Operands[1].TemplateReturnCode.Should().Be("MFCR 310");

        _audit.Verify(a => a.Log(
            "CrossSheetRule", It.IsAny<int>(), "Created",
            null, It.IsAny<CrossSheetRule>(), "admin",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // 7. AddBusinessRule — creates with all fields
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddBusinessRule_CreatesWithAllFieldsAndAudits()
    {
        // Arrange
        BusinessRule? capturedRule = null;
        _formulaRepo.Setup(r => r.AddBusinessRule(It.IsAny<BusinessRule>(), It.IsAny<CancellationToken>()))
            .Callback<BusinessRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddBusinessRule(
            "BRU-001", "Reporting Date Check", "DateCheck",
            "reporting_date <= submission_deadline",
            "[\"MFCR 300\",\"MFCR 310\"]",
            ValidationSeverity.Warning, "compliance_officer");

        // Assert
        capturedRule.Should().NotBeNull();
        capturedRule!.RuleCode.Should().Be("BRU-001");
        capturedRule.RuleName.Should().Be("Reporting Date Check");
        capturedRule.RuleType.Should().Be("DateCheck");
        capturedRule.Expression.Should().Be("reporting_date <= submission_deadline");
        capturedRule.AppliesToTemplates.Should().Be("[\"MFCR 300\",\"MFCR 310\"]");
        capturedRule.Severity.Should().Be(ValidationSeverity.Warning);
        capturedRule.IsActive.Should().BeTrue();
        capturedRule.CreatedBy.Should().Be("compliance_officer");

        _formulaRepo.Verify(r => r.AddBusinessRule(It.IsAny<BusinessRule>(), It.IsAny<CancellationToken>()), Times.Once);

        _audit.Verify(a => a.Log(
            "BusinessRule", It.IsAny<int>(), "Created",
            null, It.IsAny<BusinessRule>(), "compliance_officer",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddBusinessRule_WithNullOptionalFields_CreatesSuccessfully()
    {
        // Arrange
        BusinessRule? capturedRule = null;
        _formulaRepo.Setup(r => r.AddBusinessRule(It.IsAny<BusinessRule>(), It.IsAny<CancellationToken>()))
            .Callback<BusinessRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddBusinessRule(
            "BRU-002", "Completeness Check", "Completeness",
            null, null,
            ValidationSeverity.Error, "admin");

        // Assert
        capturedRule.Should().NotBeNull();
        capturedRule!.Expression.Should().BeNull();
        capturedRule.AppliesToTemplates.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    // 8. GetAllFormulas — returns all active
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllFormulas_ReturnsAllActiveMappedToDtos()
    {
        // Arrange
        var formulas = new List<IntraSheetFormula>
        {
            new()
            {
                Id = 1,
                TemplateVersionId = 10,
                RuleCode = "ISF-001",
                RuleName = "Total Cash",
                FormulaType = FormulaType.Sum,
                TargetFieldName = "total_cash",
                TargetLineCode = "10140",
                OperandFields = "[\"cash_notes\",\"cash_coins\"]",
                ToleranceAmount = 0m,
                Severity = ValidationSeverity.Error,
                IsActive = true
            },
            new()
            {
                Id = 2,
                TemplateVersionId = 20,
                RuleCode = "ISF-002",
                RuleName = "Custom Calc",
                FormulaType = FormulaType.Custom,
                TargetFieldName = "net_total",
                OperandFields = "[\"a\",\"b\",\"c\"]",
                CustomExpression = "a + b - c",
                ToleranceAmount = 1.00m,
                Severity = ValidationSeverity.Warning,
                IsActive = true
            }
        };

        _formulaRepo.Setup(r => r.GetAllIntraSheetFormulas(It.IsAny<CancellationToken>()))
            .ReturnsAsync(formulas);

        var service = CreateService();

        // Act
        var result = await service.GetAllFormulas();

        // Assert
        result.Should().HaveCount(2);
        result[0].RuleCode.Should().Be("ISF-001");
        result[0].FormulaType.Should().Be("Sum");
        result[0].IsActive.Should().BeTrue();
        result[1].RuleCode.Should().Be("ISF-002");
        result[1].FormulaType.Should().Be("Custom");
        result[1].CustomExpression.Should().Be("a + b - c");

        _formulaRepo.Verify(r => r.GetAllIntraSheetFormulas(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllFormulas_WhenNoFormulas_ReturnsEmptyList()
    {
        // Arrange
        _formulaRepo.Setup(r => r.GetAllIntraSheetFormulas(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntraSheetFormula>());

        var service = CreateService();

        // Act
        var result = await service.GetAllFormulas();

        // Assert
        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────
    // 9. UpdateIntraSheetFormula — updates all fields
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateIntraSheetFormula_WhenFormulaExists_UpdatesAllFieldsAndAudits()
    {
        // Arrange
        var formulaId = 42;
        var existingFormula = new IntraSheetFormula
        {
            Id = formulaId,
            TemplateVersionId = 10,
            RuleCode = "ISF-001",
            RuleName = "Old Name",
            FormulaType = FormulaType.Sum,
            TargetFieldName = "old_target",
            TargetLineCode = "10000",
            OperandFields = "[\"old_field\"]",
            CustomExpression = null,
            ToleranceAmount = 0m,
            Severity = ValidationSeverity.Error,
            IsActive = true
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFormula);
        _formulaRepo.Setup(r => r.UpdateIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.UpdateIntraSheetFormula(
            formulaId,
            "Updated Name",
            FormulaType.Custom,
            "new_target",
            "20000",
            "[\"field_a\",\"field_b\"]",
            "field_a - field_b",
            0.05m,
            ValidationSeverity.Warning,
            "editor");

        // Assert — verify the formula object was mutated correctly
        _formulaRepo.Verify(r => r.UpdateIntraSheetFormula(
            It.Is<IntraSheetFormula>(f =>
                f.Id == formulaId &&
                f.RuleName == "Updated Name" &&
                f.FormulaType == FormulaType.Custom &&
                f.TargetFieldName == "new_target" &&
                f.TargetLineCode == "20000" &&
                f.OperandFields == "[\"field_a\",\"field_b\"]" &&
                f.CustomExpression == "field_a - field_b" &&
                f.ToleranceAmount == 0.05m &&
                f.Severity == ValidationSeverity.Warning),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert — audit log captures the old state (as a DTO) and new state
        _audit.Verify(a => a.Log(
            "IntraSheetFormula", formulaId, "Updated",
            It.Is<FormulaDto>(old =>
                old.RuleName == "Old Name" &&
                old.FormulaType == "Sum" &&
                old.TargetFieldName == "old_target"),
            It.IsAny<IntraSheetFormula>(),
            "editor",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateIntraSheetFormula_ClearsOptionalFieldsWhenSetToNull()
    {
        // Arrange
        var formulaId = 7;
        var existingFormula = new IntraSheetFormula
        {
            Id = formulaId,
            TemplateVersionId = 10,
            RuleCode = "ISF-003",
            RuleName = "Original",
            FormulaType = FormulaType.Custom,
            TargetFieldName = "target",
            TargetLineCode = "50000",
            OperandFields = "[\"x\"]",
            CustomExpression = "some expression",
            ToleranceAmount = 1.0m,
            Severity = ValidationSeverity.Error,
            IsActive = true
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFormula);
        _formulaRepo.Setup(r => r.UpdateIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.UpdateIntraSheetFormula(
            formulaId, "Updated", FormulaType.Sum,
            "target", null, "[\"x\",\"y\"]",
            null, 0m, ValidationSeverity.Warning, "editor");

        // Assert
        _formulaRepo.Verify(r => r.UpdateIntraSheetFormula(
            It.Is<IntraSheetFormula>(f =>
                f.TargetLineCode == null &&
                f.CustomExpression == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // 10. UpdateIntraSheetFormula — throws when formula not found
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateIntraSheetFormula_WhenFormulaNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var formulaId = 999;
        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntraSheetFormula?)null);

        var service = CreateService();

        // Act
        var act = () => service.UpdateIntraSheetFormula(
            formulaId, "Name", FormulaType.Sum,
            "target", null, "[]",
            null, 0m, ValidationSeverity.Error, "editor");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Formula {formulaId}*not found*");

        _formulaRepo.Verify(r => r.UpdateIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.Log(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // 11. DeleteIntraSheetFormula — soft deletes and audits
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteIntraSheetFormula_WhenFormulaExists_DeletesAndAudits()
    {
        // Arrange
        var formulaId = 15;
        var existingFormula = new IntraSheetFormula
        {
            Id = formulaId,
            TemplateVersionId = 10,
            RuleCode = "ISF-005",
            RuleName = "To Be Deleted",
            FormulaType = FormulaType.Sum,
            TargetFieldName = "target_field",
            OperandFields = "[\"a\",\"b\"]",
            ToleranceAmount = 0m,
            Severity = ValidationSeverity.Error,
            IsActive = true
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFormula);
        _formulaRepo.Setup(r => r.DeleteIntraSheetFormula(formulaId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.DeleteIntraSheetFormula(formulaId, "deleter");

        // Assert
        _formulaRepo.Verify(r => r.DeleteIntraSheetFormula(formulaId, It.IsAny<CancellationToken>()), Times.Once);

        _audit.Verify(a => a.Log(
            "IntraSheetFormula", formulaId, "Deleted",
            It.Is<IntraSheetFormula>(f => f.Id == formulaId && f.RuleName == "To Be Deleted"),
            null,
            "deleter",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteIntraSheetFormula_AuditReceivesFormulaAsOldState()
    {
        // Arrange
        var formulaId = 30;
        var existingFormula = new IntraSheetFormula
        {
            Id = formulaId,
            RuleCode = "ISF-030",
            RuleName = "Audit Check",
            FormulaType = FormulaType.Difference,
            TargetFieldName = "net_value",
            OperandFields = "[\"gross\",\"deductions\"]",
            Severity = ValidationSeverity.Warning,
            IsActive = true
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFormula);
        _formulaRepo.Setup(r => r.DeleteIntraSheetFormula(formulaId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.DeleteIntraSheetFormula(formulaId, "auditor");

        // Assert — old state is the formula itself, new state is null
        _audit.Verify(a => a.Log(
            "IntraSheetFormula", formulaId, "Deleted",
            It.IsAny<IntraSheetFormula>(),
            null,
            "auditor",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────
    // 12. DeleteIntraSheetFormula — throws when not found
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteIntraSheetFormula_WhenFormulaNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var formulaId = 404;
        _formulaRepo.Setup(r => r.GetIntraSheetFormulaById(formulaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntraSheetFormula?)null);

        var service = CreateService();

        // Act
        var act = () => service.DeleteIntraSheetFormula(formulaId, "deleter");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Formula {formulaId}*not found*");

        _formulaRepo.Verify(r => r.DeleteIntraSheetFormula(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.Log(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // 13. DeleteCrossSheetRule — soft deletes
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCrossSheetRule_CallsRepositoryDeleteAndAudits()
    {
        // Arrange
        var ruleId = 8;
        _formulaRepo.Setup(r => r.DeleteCrossSheetRule(ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.DeleteCrossSheetRule(ruleId, "admin");

        // Assert
        _formulaRepo.Verify(r => r.DeleteCrossSheetRule(ruleId, It.IsAny<CancellationToken>()), Times.Once);

        _audit.Verify(a => a.Log(
            "CrossSheetRule", ruleId, "Deleted",
            null, null, "admin",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCrossSheetRule_AuditPassesBothStatesAsNull()
    {
        // Arrange
        var ruleId = 55;
        _formulaRepo.Setup(r => r.DeleteCrossSheetRule(ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        object? capturedOldState = "sentinel";
        object? capturedNewState = "sentinel";

        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int, string, object?, object?, string, CancellationToken>(
                (_, _, _, oldState, newState, _, _) =>
                {
                    capturedOldState = oldState;
                    capturedNewState = newState;
                })
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.DeleteCrossSheetRule(ruleId, "admin");

        // Assert — per the implementation, both old and new state are null
        capturedOldState.Should().BeNull();
        capturedNewState.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────
    // Additional edge-case coverage
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddIntraSheetFormula_WithCustomFormula_SetsCustomExpression()
    {
        // Arrange
        var templateId = 1;
        var versionId = 5;

        var version = new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = 1,
            Status = TemplateStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "admin"
        };

        var template = new ReturnTemplate { Id = templateId, ReturnCode = "MFCR 300", Name = "SFP" };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetById(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _formulaRepo.Setup(r => r.AddIntraSheetFormula(It.IsAny<IntraSheetFormula>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddIntraSheetFormula(
            templateId, versionId,
            "ISF-CUSTOM", "Custom Check", FormulaType.Custom,
            "net_total", "[\"a\",\"b\",\"c\"]",
            "a + b - c", 0.50m,
            ValidationSeverity.Warning, "testuser");

        // Assert
        _formulaRepo.Verify(r => r.AddIntraSheetFormula(
            It.Is<IntraSheetFormula>(f =>
                f.FormulaType == FormulaType.Custom &&
                f.CustomExpression == "a + b - c" &&
                f.ToleranceAmount == 0.50m &&
                f.Severity == ValidationSeverity.Warning),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCrossSheetRule_WithEmptyOperands_CreatesRuleWithNoOperands()
    {
        // Arrange
        var operands = new List<CrossSheetRuleOperand>();
        var expression = new CrossSheetRuleExpression
        {
            Expression = "1 = 1",
            ToleranceAmount = 0m
        };

        CrossSheetRule? capturedRule = null;
        _formulaRepo.Setup(r => r.AddCrossSheetRule(It.IsAny<CrossSheetRule>(), It.IsAny<CancellationToken>()))
            .Callback<CrossSheetRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);
        _audit.Setup(a => a.Log(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        await service.AddCrossSheetRule(
            "CSR-EMPTY", "No Operands", null,
            operands, expression,
            ValidationSeverity.Warning, "admin");

        // Assert
        capturedRule.Should().NotBeNull();
        capturedRule!.Operands.Should().BeEmpty();
        capturedRule.Description.Should().BeNull();
    }

    [Fact]
    public async Task GetIntraSheetFormulas_MapsAllFormulaTypes()
    {
        // Arrange
        var versionId = 10;
        var formulas = new List<IntraSheetFormula>
        {
            new() { Id = 1, RuleCode = "R1", RuleName = "Sum", FormulaType = FormulaType.Sum, TargetFieldName = "f1", OperandFields = "[]", IsActive = true },
            new() { Id = 2, RuleCode = "R2", RuleName = "Diff", FormulaType = FormulaType.Difference, TargetFieldName = "f2", OperandFields = "[]", IsActive = true },
            new() { Id = 3, RuleCode = "R3", RuleName = "Eq", FormulaType = FormulaType.Equals, TargetFieldName = "f3", OperandFields = "[]", IsActive = true },
            new() { Id = 4, RuleCode = "R4", RuleName = "GT", FormulaType = FormulaType.GreaterThan, TargetFieldName = "f4", OperandFields = "[]", IsActive = true },
            new() { Id = 5, RuleCode = "R5", RuleName = "Req", FormulaType = FormulaType.Required, TargetFieldName = "f5", OperandFields = "[]", IsActive = true }
        };

        _formulaRepo.Setup(r => r.GetIntraSheetFormulas(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(formulas);

        var service = CreateService();

        // Act
        var result = await service.GetIntraSheetFormulas(versionId);

        // Assert
        result[0].FormulaType.Should().Be("Sum");
        result[1].FormulaType.Should().Be("Difference");
        result[2].FormulaType.Should().Be("Equals");
        result[3].FormulaType.Should().Be("GreaterThan");
        result[4].FormulaType.Should().Be("Required");
    }
}
