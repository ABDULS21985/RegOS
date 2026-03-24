using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Validation;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Metadata.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Repositories;

public class FormulaRepositoryTests
{
    private static MetadataDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new MetadataDbContext(options);
    }

    private static FormulaRepository CreateSut(MetadataDbContext db) => new(db);

    // ──────────────────────────────────────────────────────────────────
    // UpdateCrossSheetRule — operand orphan cleanup
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateCrossSheetRule_ReplacesOperands_DeletesOrphans()
    {
        // Arrange — seed a rule with 2 operands
        var dbName = nameof(UpdateCrossSheetRule_ReplacesOperands_DeletesOrphans);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new CrossSheetRule
            {
                RuleCode = "XS-100",
                RuleName = "Original",
                Severity = ValidationSeverity.Error,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
                Expression = new CrossSheetRuleExpression
                {
                    Expression = "A = B",
                    ToleranceAmount = 0.01m,
                    ErrorMessage = "Mismatch"
                }
            };
            rule.SetOperands(new List<CrossSheetRuleOperand>
            {
                new() { OperandAlias = "A", TemplateReturnCode = "T1", FieldName = "f1", SortOrder = 1 },
                new() { OperandAlias = "B", TemplateReturnCode = "T2", FieldName = "f2", SortOrder = 2 }
            });

            seedDb.CrossSheetRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        // Act — load, replace operands (remove B, add C), and update
        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            var rule = await actDb.CrossSheetRules
                .Include(r => r.Operands)
                .Include(r => r.Expression)
                .FirstAsync(r => r.Id == ruleId);

            rule.RuleName = "Updated";
            rule.SetOperands(new List<CrossSheetRuleOperand>
            {
                new() { OperandAlias = "A", TemplateReturnCode = "T1", FieldName = "f1_updated", SortOrder = 1 },
                new() { OperandAlias = "C", TemplateReturnCode = "T3", FieldName = "f3", SortOrder = 2 }
            });

            await sut.UpdateCrossSheetRule(rule);
        }

        // Assert — verify via fresh context
        await using var assertDb = CreateDbContext(dbName);
        var updated = await assertDb.CrossSheetRules
            .Include(r => r.Operands)
            .Include(r => r.Expression)
            .FirstAsync(r => r.Id == ruleId);

        updated.RuleName.Should().Be("Updated");
        updated.Operands.Should().HaveCount(2);
        updated.Operands.Select(o => o.OperandAlias).Should().BeEquivalentTo("A", "C");
        updated.Operands.First(o => o.OperandAlias == "A").FieldName.Should().Be("f1_updated");
        updated.Operands.First(o => o.OperandAlias == "C").TemplateReturnCode.Should().Be("T3");

        // Orphan "B" should be gone
        var allOperands = await assertDb.CrossSheetRuleOperands.Where(o => o.RuleId == ruleId).ToListAsync();
        allOperands.Select(o => o.OperandAlias).Should().NotContain("B");
    }

    [Fact]
    public async Task UpdateCrossSheetRule_ReplacesExpression()
    {
        // Arrange
        var dbName = nameof(UpdateCrossSheetRule_ReplacesExpression);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new CrossSheetRule
            {
                RuleCode = "XS-200",
                RuleName = "Expr Test",
                Severity = ValidationSeverity.Warning,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
                Expression = new CrossSheetRuleExpression
                {
                    Expression = "A = B",
                    ToleranceAmount = 0.01m
                }
            };
            rule.SetOperands(new List<CrossSheetRuleOperand>
            {
                new() { OperandAlias = "A", TemplateReturnCode = "T1", FieldName = "f1", SortOrder = 1 }
            });
            seedDb.CrossSheetRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        // Act — replace expression with new one (Id = 0 means new)
        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            var rule = await actDb.CrossSheetRules
                .Include(r => r.Operands)
                .Include(r => r.Expression)
                .FirstAsync(r => r.Id == ruleId);

            rule.Expression = new CrossSheetRuleExpression
            {
                Expression = "A >= B * 0.95",
                ToleranceAmount = 0.05m,
                TolerancePercent = 5.0m,
                ErrorMessage = "Threshold exceeded"
            };

            await sut.UpdateCrossSheetRule(rule);
        }

        // Assert
        await using var assertDb = CreateDbContext(dbName);
        var updated = await assertDb.CrossSheetRules
            .Include(r => r.Expression)
            .FirstAsync(r => r.Id == ruleId);

        updated.Expression.Should().NotBeNull();
        updated.Expression!.Expression.Should().Be("A >= B * 0.95");
        updated.Expression.ToleranceAmount.Should().Be(0.05m);
        updated.Expression.TolerancePercent.Should().Be(5.0m);
        updated.Expression.ErrorMessage.Should().Be("Threshold exceeded");

        // Only one expression row should exist for this rule
        var exprCount = await assertDb.CrossSheetRuleExpressions.CountAsync(e => e.RuleId == ruleId);
        exprCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateCrossSheetRule_KeepsExistingOperands_WhenNotReplaced()
    {
        // Arrange
        var dbName = nameof(UpdateCrossSheetRule_KeepsExistingOperands_WhenNotReplaced);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new CrossSheetRule
            {
                RuleCode = "XS-300",
                RuleName = "Keep Test",
                Severity = ValidationSeverity.Error,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
                Expression = new CrossSheetRuleExpression
                {
                    Expression = "A + B = C",
                    ToleranceAmount = 0m
                }
            };
            rule.SetOperands(new List<CrossSheetRuleOperand>
            {
                new() { OperandAlias = "A", TemplateReturnCode = "T1", FieldName = "f1", SortOrder = 1 },
                new() { OperandAlias = "B", TemplateReturnCode = "T2", FieldName = "f2", SortOrder = 2 },
                new() { OperandAlias = "C", TemplateReturnCode = "T3", FieldName = "f3", SortOrder = 3 }
            });
            seedDb.CrossSheetRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        // Act — just update RuleName, keep operands the same
        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            var rule = await actDb.CrossSheetRules
                .Include(r => r.Operands)
                .Include(r => r.Expression)
                .FirstAsync(r => r.Id == ruleId);

            rule.RuleName = "Renamed";
            // Don't call SetOperands — keep existing

            await sut.UpdateCrossSheetRule(rule);
        }

        // Assert
        await using var assertDb = CreateDbContext(dbName);
        var updated = await assertDb.CrossSheetRules
            .Include(r => r.Operands)
            .FirstAsync(r => r.Id == ruleId);

        updated.RuleName.Should().Be("Renamed");
        updated.Operands.Should().HaveCount(3);
        updated.Operands.Select(o => o.OperandAlias).Should().BeEquivalentTo("A", "B", "C");
    }

    // ──────────────────────────────────────────────────────────────────
    // DeleteCrossSheetRule — soft delete
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCrossSheetRule_SetsIsActiveFalse()
    {
        var dbName = nameof(DeleteCrossSheetRule_SetsIsActiveFalse);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new CrossSheetRule
            {
                RuleCode = "XS-DEL",
                RuleName = "To Delete",
                Severity = ValidationSeverity.Error,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            seedDb.CrossSheetRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            await sut.DeleteCrossSheetRule(ruleId);
        }

        await using var assertDb = CreateDbContext(dbName);
        var deleted = await assertDb.CrossSheetRules.FirstAsync(r => r.Id == ruleId);
        deleted.IsActive.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────
    // BusinessRule — GetAllBusinessRules includes inactive
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBusinessRules_IncludesInactiveRules()
    {
        var dbName = nameof(GetAllBusinessRules_IncludesInactiveRules);
        await using (var seedDb = CreateDbContext(dbName))
        {
            seedDb.BusinessRules.AddRange(
                new BusinessRule { RuleCode = "BR-1", RuleName = "Active", RuleType = "Custom", Severity = ValidationSeverity.Error, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" },
                new BusinessRule { RuleCode = "BR-2", RuleName = "Inactive", RuleType = "Custom", Severity = ValidationSeverity.Warning, IsActive = false, CreatedAt = DateTime.UtcNow, CreatedBy = "seed" }
            );
            await seedDb.SaveChangesAsync();
        }

        await using var actDb = CreateDbContext(dbName);
        var sut = CreateSut(actDb);
        var rules = await sut.GetAllBusinessRules();

        rules.Should().HaveCount(2);
        rules.First().IsActive.Should().BeTrue("active rules sort first");
    }

    // ──────────────────────────────────────────────────────────────────
    // BusinessRule — DeleteBusinessRule soft-deletes
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBusinessRule_SetsIsActiveFalse()
    {
        var dbName = nameof(DeleteBusinessRule_SetsIsActiveFalse);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new BusinessRule
            {
                RuleCode = "BR-DEL",
                RuleName = "To Delete",
                RuleType = "Completeness",
                Severity = ValidationSeverity.Error,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            seedDb.BusinessRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            await sut.DeleteBusinessRule(ruleId);
        }

        await using var assertDb = CreateDbContext(dbName);
        var deleted = await assertDb.BusinessRules.FirstAsync(r => r.Id == ruleId);
        deleted.IsActive.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────
    // BusinessRule — UpdateBusinessRule persists all fields
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBusinessRule_PersistsAllFields()
    {
        var dbName = nameof(UpdateBusinessRule_PersistsAllFields);
        int ruleId;
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new BusinessRule
            {
                RuleCode = "BR-UPD",
                RuleName = "Original",
                RuleType = "Completeness",
                Description = null,
                Expression = null,
                AppliesToTemplates = null,
                Severity = ValidationSeverity.Warning,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            seedDb.BusinessRules.Add(rule);
            await seedDb.SaveChangesAsync();
            ruleId = rule.Id;
        }

        await using (var actDb = CreateDbContext(dbName))
        {
            var sut = CreateSut(actDb);
            var rule = await actDb.BusinessRules.FindAsync(ruleId);
            rule!.RuleName = "Updated";
            rule.RuleType = "Custom";
            rule.Description = "A custom rule";
            rule.Expression = "total_assets > 0";
            rule.AppliesToTemplates = "[\"MFCR 300\",\"BSL 100\"]";
            rule.Severity = ValidationSeverity.Error;
            await sut.UpdateBusinessRule(rule);
        }

        await using var assertDb = CreateDbContext(dbName);
        var updated = await assertDb.BusinessRules.FirstAsync(r => r.Id == ruleId);
        updated.RuleName.Should().Be("Updated");
        updated.RuleType.Should().Be("Custom");
        updated.Description.Should().Be("A custom rule");
        updated.Expression.Should().Be("total_assets > 0");
        updated.AppliesToTemplates.Should().Be("[\"MFCR 300\",\"BSL 100\"]");
        updated.Severity.Should().Be(ValidationSeverity.Error);
    }

    // ──────────────────────────────────────────────────────────────────
    // GetAllCrossSheetRules — includes operands and expression
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCrossSheetRules_IncludesOperandsAndExpression()
    {
        var dbName = nameof(GetAllCrossSheetRules_IncludesOperandsAndExpression);
        await using (var seedDb = CreateDbContext(dbName))
        {
            var rule = new CrossSheetRule
            {
                RuleCode = "XS-INC",
                RuleName = "Include Test",
                Severity = ValidationSeverity.Error,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed",
                Expression = new CrossSheetRuleExpression
                {
                    Expression = "A = B",
                    ToleranceAmount = 0.01m
                }
            };
            rule.SetOperands(new List<CrossSheetRuleOperand>
            {
                new() { OperandAlias = "A", TemplateReturnCode = "T1", FieldName = "f1", SortOrder = 1 },
                new() { OperandAlias = "B", TemplateReturnCode = "T2", FieldName = "f2", SortOrder = 2 }
            });
            seedDb.CrossSheetRules.Add(rule);
            await seedDb.SaveChangesAsync();
        }

        await using var actDb = CreateDbContext(dbName);
        var sut = CreateSut(actDb);
        var rules = await sut.GetAllCrossSheetRules();

        rules.Should().HaveCount(1);
        rules[0].Operands.Should().HaveCount(2);
        rules[0].Expression.Should().NotBeNull();
        rules[0].Expression!.Expression.Should().Be("A = B");
    }
}
