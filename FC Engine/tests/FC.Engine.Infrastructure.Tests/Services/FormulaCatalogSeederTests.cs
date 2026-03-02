using System.Text.Json;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FluentAssertions;
using Moq;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class FormulaCatalogSeederTests
{
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IFormulaRepository> _formulaRepo = new();

    private FormulaCatalogSeeder CreateSeeder() => new(_templateRepo.Object, _formulaRepo.Object);

    [Fact]
    public async Task SeedFromCatalog_WithValidFormulas_CreatesIntraSheetFormulas()
    {
        // Arrange — create a template with fields that have line codes matching the catalog
        var version = new TemplateVersion
        {
            Id = 1,
            TemplateId = 1,
            VersionNumber = 1,
            Status = TemplateStatus.Published,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        version.SetFields(new List<TemplateField>
        {
            new() { FieldName = "cash_notes", LineCode = "10110", DisplayName = "Notes" },
            new() { FieldName = "cash_coins", LineCode = "10120", DisplayName = "Coins" },
            new() { FieldName = "total_cash", LineCode = "10140", DisplayName = "TOTAL CASH" }
        });

        var template = new ReturnTemplate
        {
            Id = 1,
            ReturnCode = "MFCR 300",
            Name = "Statement of Financial Position",
            PhysicalTableName = "mfcr_300",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetByReturnCode("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _templateRepo.Setup(r => r.Update(It.IsAny<ReturnTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var catalog = new FormulaCatalog
        {
            IntraSheetFormulas = new List<CatalogIntraSheetFormula>
            {
                new()
                {
                    ReturnCode = "MFCR 300",
                    TargetItemCode = "10140",
                    TargetDescription = "TOTAL CASH",
                    FormulaExpression = "10110+10120",
                    FormulaType = "Sum",
                    OperandItemCodes = new List<string> { "10110", "10120" },
                    Row = 14
                }
            }
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));

        try
        {
            // Act
            var seeder = CreateSeeder();
            var result = await seeder.SeedFromCatalog(tempFile, "test");

            // Assert
            result.TotalFormulasCreated.Should().Be(1);
            result.TemplatesSeeded.Should().Contain("MFCR 300");
            result.Errors.Should().BeEmpty();

            // Verify the formula was added to the version
            version.IntraSheetFormulas.Should().HaveCount(1);
            var formula = version.IntraSheetFormulas[0];
            formula.TargetFieldName.Should().Be("total_cash");
            formula.TargetLineCode.Should().Be("10140");
            formula.FormulaType.Should().Be(FormulaType.Sum);
            formula.RuleName.Should().Be("TOTAL CASH");

            var operands = JsonSerializer.Deserialize<List<string>>(formula.OperandFields);
            operands.Should().BeEquivalentTo(new[] { "cash_notes", "cash_coins" });

            _templateRepo.Verify(r => r.Update(template, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SeedFromCatalog_WithCustomType_CreatesCustomExpression()
    {
        var version = new TemplateVersion
        {
            Id = 1, TemplateId = 1, VersionNumber = 1,
            Status = TemplateStatus.Published,
            CreatedAt = DateTime.UtcNow, CreatedBy = "test"
        };
        version.SetFields(new List<TemplateField>
        {
            new() { FieldName = "bills_fvtpl", LineCode = "10420", DisplayName = "Treasury Bills FVTPL" },
            new() { FieldName = "bills_fvoci", LineCode = "10430", DisplayName = "Treasury Bills FVOCI" },
            new() { FieldName = "bills_amortised", LineCode = "10440", DisplayName = "Treasury Bills Amortised" },
            new() { FieldName = "impairment_bills", LineCode = "10510", DisplayName = "Impairment" },
            new() { FieldName = "net_treasury_bills", LineCode = "10520", DisplayName = "Net Treasury Bills" }
        });

        var template = new ReturnTemplate
        {
            Id = 1, ReturnCode = "MFCR 300", Name = "SFP",
            PhysicalTableName = "mfcr_300",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetByReturnCode("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _templateRepo.Setup(r => r.Update(It.IsAny<ReturnTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var catalog = new FormulaCatalog
        {
            IntraSheetFormulas = new List<CatalogIntraSheetFormula>
            {
                new()
                {
                    ReturnCode = "MFCR 300",
                    TargetItemCode = "10520",
                    TargetDescription = "Net Treasury Bills",
                    FormulaExpression = "10420+10430+10440-10510",
                    FormulaType = "Custom",
                    OperandItemCodes = new List<string> { "10420", "10430", "10440", "10510" },
                    Row = 50
                }
            }
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));

        try
        {
            var seeder = CreateSeeder();
            var result = await seeder.SeedFromCatalog(tempFile, "test");

            result.TotalFormulasCreated.Should().Be(1);
            var formula = version.IntraSheetFormulas[0];
            formula.FormulaType.Should().Be(FormulaType.Custom);
            formula.CustomExpression.Should().Contain("bills_fvtpl");
            formula.CustomExpression.Should().Contain("-");
            formula.CustomExpression.Should().Contain("impairment_bills");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SeedFromCatalog_SkipsAlreadySeededTemplates()
    {
        var version = new TemplateVersion
        {
            Id = 1, TemplateId = 1, VersionNumber = 1,
            Status = TemplateStatus.Published,
            CreatedAt = DateTime.UtcNow, CreatedBy = "test"
        };
        version.SetFields(new List<TemplateField>
        {
            new() { FieldName = "total_cash", LineCode = "10140" },
        });
        // Pre-add a formula so it's already seeded
        version.AddFormula(new IntraSheetFormula
        {
            RuleCode = "EXISTING", RuleName = "Existing",
            FormulaType = FormulaType.Sum, TargetFieldName = "total_cash",
            OperandFields = "[]", IsActive = true
        });

        var template = new ReturnTemplate
        {
            Id = 1, ReturnCode = "MFCR 300", Name = "SFP",
            PhysicalTableName = "mfcr_300",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetByReturnCode("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var catalog = new FormulaCatalog
        {
            IntraSheetFormulas = new List<CatalogIntraSheetFormula>
            {
                new()
                {
                    ReturnCode = "MFCR 300", TargetItemCode = "10140",
                    TargetDescription = "TOTAL CASH", FormulaExpression = "10110+10120",
                    FormulaType = "Sum", OperandItemCodes = new() { "10110", "10120" }
                }
            }
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));

        try
        {
            var seeder = CreateSeeder();
            var result = await seeder.SeedFromCatalog(tempFile, "test");

            result.TotalFormulasCreated.Should().Be(0);
            result.Skipped.Should().Contain("MFCR 300");

            _templateRepo.Verify(r => r.Update(It.IsAny<ReturnTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SeedFromCatalog_MissingTemplate_RecordsError()
    {
        _templateRepo.Setup(r => r.GetByReturnCode("MFCR 999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReturnTemplate?)null);

        var catalog = new FormulaCatalog
        {
            IntraSheetFormulas = new List<CatalogIntraSheetFormula>
            {
                new()
                {
                    ReturnCode = "MFCR 999", TargetItemCode = "99999",
                    TargetDescription = "Missing", FormulaExpression = "11111+22222",
                    FormulaType = "Sum", OperandItemCodes = new() { "11111", "22222" }
                }
            }
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));

        try
        {
            var seeder = CreateSeeder();
            var result = await seeder.SeedFromCatalog(tempFile, "test");

            result.Errors.Should().ContainSingle().Which.Should().Contain("MFCR 999");
            result.TotalFormulasCreated.Should().Be(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SeedFromCatalog_DifferenceType_CreatesCorrectFormulaType()
    {
        var version = new TemplateVersion
        {
            Id = 1, TemplateId = 1, VersionNumber = 1,
            Status = TemplateStatus.Published,
            CreatedAt = DateTime.UtcNow, CreatedBy = "test"
        };
        version.SetFields(new List<TemplateField>
        {
            new() { FieldName = "gross_loans", LineCode = "11910", DisplayName = "Total Loans" },
            new() { FieldName = "impairment_loans", LineCode = "11920", DisplayName = "Impairment" },
            new() { FieldName = "net_loans", LineCode = "11930", DisplayName = "Net Loans" }
        });

        var template = new ReturnTemplate
        {
            Id = 1, ReturnCode = "MFCR 300", Name = "SFP",
            PhysicalTableName = "mfcr_300",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
        };
        template.AddVersion(version);

        _templateRepo.Setup(r => r.GetByReturnCode("MFCR 300", It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _templateRepo.Setup(r => r.Update(It.IsAny<ReturnTemplate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var catalog = new FormulaCatalog
        {
            IntraSheetFormulas = new List<CatalogIntraSheetFormula>
            {
                new()
                {
                    ReturnCode = "MFCR 300", TargetItemCode = "11930",
                    TargetDescription = "Net Loans", FormulaExpression = "11910-11920",
                    FormulaType = "Difference", OperandItemCodes = new() { "11910", "11920" }
                }
            }
        };

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));

        try
        {
            var seeder = CreateSeeder();
            var result = await seeder.SeedFromCatalog(tempFile, "test");

            result.TotalFormulasCreated.Should().Be(1);
            var formula = version.IntraSheetFormulas[0];
            formula.FormulaType.Should().Be(FormulaType.Difference);
            formula.TargetFieldName.Should().Be("net_loans");

            var operands = JsonSerializer.Deserialize<List<string>>(formula.OperandFields);
            operands.Should().BeEquivalentTo(new[] { "gross_loans", "impairment_loans" });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
