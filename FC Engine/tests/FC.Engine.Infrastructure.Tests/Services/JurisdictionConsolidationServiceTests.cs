using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

using FC.Engine.Infrastructure.Tests;

namespace FC.Engine.Infrastructure.Tests.Services;

public class JurisdictionConsolidationServiceTests
{
    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }

    [Fact]
    public async Task GetConsolidation_Aggregates_Across_Jurisdictions_With_Fx_Conversion()
    {
        using var db = CreateDb();

        var holding = Tenant.Create("Holding", "holding", TenantType.HoldingGroup);
        var ngChild = Tenant.Create("NG Child", "ng-child", TenantType.Institution);
        var ghChild = Tenant.Create("GH Child", "gh-child", TenantType.Institution);
        ngChild.SetParentTenant(holding.TenantId);
        ghChild.SetParentTenant(holding.TenantId);

        db.Tenants.AddRange(holding, ngChild, ghChild);
        db.Jurisdictions.AddRange(
            new Jurisdiction
            {
                Id = 1,
                CountryCode = "NG",
                CountryName = "Nigeria",
                Currency = "NGN",
                Timezone = "Africa/Lagos",
                RegulatoryBodies = "[]",
                DateFormat = "dd/MM/yyyy",
                DataResidencyRegion = "SouthAfricaNorth",
                IsActive = true
            },
            new Jurisdiction
            {
                Id = 2,
                CountryCode = "GH",
                CountryName = "Ghana",
                Currency = "GHS",
                Timezone = "Africa/Accra",
                RegulatoryBodies = "[]",
                DateFormat = "dd/MM/yyyy",
                DataProtectionLaw = "Ghana DPA 2012",
                DataResidencyRegion = "WestEurope",
                IsActive = false
            });

        db.Institutions.AddRange(
            new Institution
            {
                TenantId = ngChild.TenantId,
                JurisdictionId = 1,
                InstitutionCode = "NG01",
                InstitutionName = "Nigeria Entity",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Institution
            {
                TenantId = ghChild.TenantId,
                JurisdictionId = 2,
                InstitutionCode = "GH01",
                InstitutionName = "Ghana Entity",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

        db.Invoices.AddRange(
            new Invoice
            {
                TenantId = ngChild.TenantId,
                InvoiceNumber = "INV-NG-1",
                SubscriptionId = 1,
                PeriodStart = new DateOnly(2026, 1, 1),
                PeriodEnd = new DateOnly(2026, 1, 31),
                Subtotal = 100m,
                VatAmount = 0m,
                TotalAmount = 100m,
                Currency = "NGN",
                Status = InvoiceStatus.Issued
            },
            new Invoice
            {
                TenantId = ghChild.TenantId,
                InvoiceNumber = "INV-GH-1",
                SubscriptionId = 2,
                PeriodStart = new DateOnly(2026, 1, 1),
                PeriodEnd = new DateOnly(2026, 1, 31),
                Subtotal = 200m,
                VatAmount = 0m,
                TotalAmount = 200m,
                Currency = "GHS",
                Status = InvoiceStatus.Issued
            });

        db.JurisdictionFxRates.Add(new JurisdictionFxRate
        {
            BaseCurrency = "GHS",
            QuoteCurrency = "NGN",
            Rate = 10m,
            RateDate = new DateOnly(2026, 3, 1),
            Source = "Manual"
        });

        await db.SaveChangesAsync();

        var sut = new JurisdictionConsolidationService(new TestDbContextFactory(db));
        var result = await sut.GetConsolidation(holding.TenantId, "NGN");

        result.SubsidiaryCount.Should().Be(2);
        result.Jurisdictions.Should().HaveCount(2);
        result.GrossAmount.Should().Be(2100m); // 100 NGN + (200 GHS * 10)
        result.NetAmount.Should().Be(2100m);
    }

    [Fact]
    public async Task GetConsolidation_Applies_Elimination_Adjustments()
    {
        using var db = CreateDb();

        var holding = Tenant.Create("Holding", "holding-2", TenantType.HoldingGroup);
        var child = Tenant.Create("Child", "child-2", TenantType.Institution);
        child.SetParentTenant(holding.TenantId);

        db.Tenants.AddRange(holding, child);
        db.Jurisdictions.Add(new Jurisdiction
        {
            Id = 1,
            CountryCode = "NG",
            CountryName = "Nigeria",
            Currency = "NGN",
            Timezone = "Africa/Lagos",
            RegulatoryBodies = "[]",
            DateFormat = "dd/MM/yyyy",
            DataResidencyRegion = "SouthAfricaNorth",
            IsActive = true
        });
        db.Institutions.Add(new Institution
        {
            TenantId = child.TenantId,
            JurisdictionId = 1,
            InstitutionCode = "NG02",
            InstitutionName = "Nigeria Child",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Invoices.Add(new Invoice
        {
            TenantId = child.TenantId,
            InvoiceNumber = "INV-1",
            SubscriptionId = 1,
            PeriodStart = new DateOnly(2026, 1, 1),
            PeriodEnd = new DateOnly(2026, 1, 31),
            Subtotal = 1000m,
            VatAmount = 0m,
            TotalAmount = 1000m,
            Currency = "NGN",
            Status = InvoiceStatus.Issued
        });
        db.ConsolidationAdjustments.Add(new ConsolidationAdjustment
        {
            TenantId = holding.TenantId,
            AdjustmentType = "Elimination",
            Amount = 150m,
            Currency = "NGN",
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Description = "Intercompany elimination"
        });
        await db.SaveChangesAsync();

        var sut = new JurisdictionConsolidationService(new TestDbContextFactory(db));
        var result = await sut.GetConsolidation(holding.TenantId, "NGN");

        result.GrossAmount.Should().Be(1000m);
        result.EliminationAdjustments.Should().Be(150m);
        result.NetAmount.Should().Be(850m);
    }
}
