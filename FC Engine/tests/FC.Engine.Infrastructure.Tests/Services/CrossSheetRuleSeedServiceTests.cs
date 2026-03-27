using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Validation;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class CrossSheetRuleSeedServiceTests
{
    [Fact]
    public async Task SeedCrossSheetRules_SeedsCorrectedCatalogDefinitions()
    {
        var rules = new List<CrossSheetRule>();
        var formulaRepo = BuildRepository(rules);
        var sut = new CrossSheetRuleSeedService(formulaRepo.Object);

        var changes = await sut.SeedCrossSheetRules("seed-user");

        changes.Should().Be(rules.Count);

        var xs007 = rules.Single(rule => rule.RuleCode == "XS-007");
        xs007.Expression.Should().NotBeNull();
        xs007.Expression!.Expression.Should().Be("A = B");
        xs007.Operands.Should().ContainSingle(operand =>
            operand.OperandAlias == "B" &&
            operand.TemplateReturnCode == "MFCR 362" &&
            operand.FieldName == "net_carrying_amount" &&
            operand.AggregateFunction == "SUM");
        xs007.Operands.Should().HaveCount(2);

        var xs036 = rules.Single(rule => rule.RuleCode == "XS-036");
        xs036.RuleName.Should().Be("Direct Credit Substitutes Total");
        xs036.Expression.Should().NotBeNull();
        xs036.Expression!.Expression.Should().Be("A >= 0");
        xs036.Operands.Should().ContainSingle(operand =>
            operand.OperandAlias == "A" &&
            operand.TemplateReturnCode == "QFCR 364" &&
            operand.FieldName == "amount" &&
            operand.AggregateFunction == "SUM");
        xs036.Operands.Should().HaveCount(1);
    }

    [Fact]
    public async Task SeedCrossSheetRules_RepairsStaleRulesInPlace_AndThenBecomesIdempotent()
    {
        var rules = new List<CrossSheetRule>();
        var updateCount = 0;
        var addCount = 0;
        var formulaRepo = BuildRepository(
            rules,
            onAdd: _ => addCount++,
            onUpdate: _ => updateCount++);
        var sut = new CrossSheetRuleSeedService(formulaRepo.Object);

        await sut.SeedCrossSheetRules("initial-seed");
        var initialAddCount = addCount;

        var xs007 = rules.Single(rule => rule.RuleCode == "XS-007");
        xs007.SetOperands(new List<CrossSheetRuleOperand>
        {
            new()
            {
                OperandAlias = "A",
                TemplateReturnCode = "MFCR 300",
                FieldName = "property_plant_equipment",
                SortOrder = 1
            },
            new()
            {
                OperandAlias = "B",
                TemplateReturnCode = "MFCR 362",
                FieldName = "carrying_end_naira",
                AggregateFunction = "SUM",
                SortOrder = 2
            },
            new()
            {
                OperandAlias = "C",
                TemplateReturnCode = "MFCR 362",
                FieldName = "carrying_end_foreign",
                AggregateFunction = "SUM",
                SortOrder = 3
            }
        });
        xs007.Expression = new CrossSheetRuleExpression
        {
            Expression = "A = B + C",
            ToleranceAmount = 0.01m,
            ErrorMessage = "Property Plant Equipment Reconciliation: PPE on balance sheet should match MFCR 362 schedule"
        };

        var repairChanges = await sut.SeedCrossSheetRules("repair-user");

        repairChanges.Should().BeGreaterThan(0);
        updateCount.Should().BeGreaterThan(0);
        addCount.Should().Be(initialAddCount);
        xs007.Expression.Should().NotBeNull();
        xs007.Expression!.Expression.Should().Be("A = B");
        xs007.Operands.Should().ContainSingle(operand =>
            operand.OperandAlias == "B" &&
            operand.FieldName == "net_carrying_amount");
        xs007.Operands.Should().HaveCount(2);

        var updateCountAfterRepair = updateCount;
        var thirdRunChanges = await sut.SeedCrossSheetRules("repair-user");

        thirdRunChanges.Should().Be(0);
        updateCount.Should().Be(updateCountAfterRepair);
        addCount.Should().Be(initialAddCount);
    }

    private static Mock<IFormulaRepository> BuildRepository(
        List<CrossSheetRule> rules,
        Action<CrossSheetRule>? onAdd = null,
        Action<CrossSheetRule>? onUpdate = null)
    {
        var formulaRepo = new Mock<IFormulaRepository>(MockBehavior.Strict);

        formulaRepo
            .Setup(repo => repo.GetAllCrossSheetRules(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => rules.ToList());

        formulaRepo
            .Setup(repo => repo.AddCrossSheetRule(It.IsAny<CrossSheetRule>(), It.IsAny<CancellationToken>()))
            .Returns<CrossSheetRule, CancellationToken>((rule, _) =>
            {
                rules.Add(rule);
                onAdd?.Invoke(rule);
                return Task.CompletedTask;
            });

        formulaRepo
            .Setup(repo => repo.UpdateCrossSheetRule(It.IsAny<CrossSheetRule>(), It.IsAny<CancellationToken>()))
            .Returns<CrossSheetRule, CancellationToken>((rule, _) =>
            {
                onUpdate?.Invoke(rule);
                return Task.CompletedTask;
            });

        return formulaRepo;
    }
}
