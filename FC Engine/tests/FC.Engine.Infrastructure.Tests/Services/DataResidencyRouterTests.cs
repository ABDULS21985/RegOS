using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using FC.Engine.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FC.Engine.Infrastructure.Tests.Services;

public class DataResidencyRouterTests
{
    private static MetadataDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MetadataDbContext(options);
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FcEngine"] = "Server=default;Database=fc;",
                ["ConnectionStrings:FcEngineNigeria"] = "Server=ng;Database=fc_ng;",
                ["ConnectionStrings:FcEngineGhana"] = "Server=gh;Database=fc_gh;",
                ["DataResidency:DefaultRegion"] = "SouthAfricaNorth",
                ["DataResidency:MultiJurisdictionRegion"] = "WestEurope",
                ["DataResidency:RegionConnectionStrings:SouthAfricaNorth"] = "FcEngineNigeria",
                ["DataResidency:RegionConnectionStrings:WestEurope"] = "FcEngineGhana"
            })
            .Build();

    [Fact]
    public async Task ResolveRegion_Returns_Single_Jurisdiction_Region()
    {
        using var db = CreateDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = BuildConfig();

        var tenant = Tenant.Create("Tenant NG", "tenant-ng", TenantType.Institution);
        db.Tenants.Add(tenant);
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
            TenantId = tenant.TenantId,
            JurisdictionId = 1,
            InstitutionCode = "NG001",
            InstitutionName = "NG Institution",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new DataResidencyRouter(db, config, cache);
        var region = await sut.ResolveRegion(tenant.TenantId);

        region.Should().Be("SouthAfricaNorth");
    }

    [Fact]
    public async Task ResolveConnectionString_Uses_Region_Mapping()
    {
        using var db = CreateDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = BuildConfig();

        var tenant = Tenant.Create("Tenant GH", "tenant-gh", TenantType.Institution);
        db.Tenants.Add(tenant);
        db.Jurisdictions.Add(new Jurisdiction
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
        db.Institutions.Add(new Institution
        {
            TenantId = tenant.TenantId,
            JurisdictionId = 2,
            InstitutionCode = "GH001",
            InstitutionName = "GH Institution",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new DataResidencyRouter(db, config, cache);
        var connectionString = await sut.ResolveConnectionString(tenant.TenantId);

        connectionString.Should().Be("Server=gh;Database=fc_gh;");
    }

    [Fact]
    public async Task ResolveRegion_Uses_MultiJurisdiction_Fallback_For_Holding_Tenant()
    {
        using var db = CreateDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = BuildConfig();

        var tenant = Tenant.Create("Holding Tenant", "holding", TenantType.HoldingGroup);
        db.Tenants.Add(tenant);
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
                DataResidencyRegion = "WestEurope",
                IsActive = false
            });
        db.Institutions.AddRange(
            new Institution
            {
                TenantId = tenant.TenantId,
                JurisdictionId = 1,
                InstitutionCode = "NG001",
                InstitutionName = "NG Subsidiary",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Institution
            {
                TenantId = tenant.TenantId,
                JurisdictionId = 2,
                InstitutionCode = "GH001",
                InstitutionName = "GH Subsidiary",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new DataResidencyRouter(db, config, cache);
        var region = await sut.ResolveRegion(tenant.TenantId);

        region.Should().Be("WestEurope");
    }

    [Fact]
    public async Task ResolveConnectionString_Handles_Region_Keys_With_Spaces()
    {
        using var db = CreateDb();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FcEngine"] = "Server=default;Database=fc;",
                ["ConnectionStrings:FcEngineUae"] = "Server=uae;Database=fc_uae;",
                ["DataResidency:DefaultRegion"] = "SouthAfricaNorth",
                ["DataResidency:RegionConnectionStrings:UAENorth"] = "FcEngineUae"
            })
            .Build();

        var tenant = Tenant.Create("Tenant KE", "tenant-ke", TenantType.Institution);
        db.Tenants.Add(tenant);
        db.Jurisdictions.Add(new Jurisdiction
        {
            Id = 3,
            CountryCode = "KE",
            CountryName = "Kenya",
            Currency = "KES",
            Timezone = "Africa/Nairobi",
            RegulatoryBodies = "[]",
            DateFormat = "dd/MM/yyyy",
            DataProtectionLaw = "Kenya DPA 2019",
            DataResidencyRegion = "UAE North",
            IsActive = false
        });
        db.Institutions.Add(new Institution
        {
            TenantId = tenant.TenantId,
            JurisdictionId = 3,
            InstitutionCode = "KE001",
            InstitutionName = "KE Institution",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new DataResidencyRouter(db, config, cache);
        var connectionString = await sut.ResolveConnectionString(tenant.TenantId);

        connectionString.Should().Be("Server=uae;Database=fc_uae;");
    }
}
