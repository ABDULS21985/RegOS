using FC.Engine.Admin.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Admin.Tests.Services;

public class DashboardBriefingPackBuilderTests
{
    [Fact]
    public void Build_ForGovernor_Includes_Freshness_Section_When_Catalogs_Are_Stale()
    {
        var workspace = new PlatformIntelligenceWorkspace
        {
            Refresh = new PlatformIntelligenceRefreshSnapshot
            {
                CatalogFreshness =
                [
                    new PlatformIntelligenceCatalogFreshnessRow
                    {
                        Area = "Knowledge",
                        Artifact = "Compliance dossier",
                        Status = "Stale",
                        AgeLabel = "29h old",
                        ThresholdLabel = "<= 6h",
                        Commentary = "The dossier has not been rebuilt since the last rules change."
                    }
                ]
            }
        };

        var sut = new DashboardBriefingPackBuilder();

        var sections = sut.Build(workspace, "governor", null, null, null, null);

        sections.Should().HaveCount(6);
        sections.Should().ContainSingle(x => x.SectionCode == "GOV-06")
            .Which.Should().BeEquivalentTo(new
            {
                SectionName = "Intelligence Freshness",
                Coverage = "1 stale | 0 watch | 0 pending",
                Signal = "Critical"
            });
        sections.Should().Contain(x =>
            x.SectionCode == "GOV-06"
            && x.Commentary.Contains("Compliance dossier", StringComparison.Ordinal)
            && x.Commentary.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ForExecutive_Includes_Freshness_Section_Without_Telemetry()
    {
        var workspace = new PlatformIntelligenceWorkspace
        {
            InstitutionDetails =
            [
                new InstitutionIntelligenceDetail
                {
                    InstitutionId = 11,
                    InstitutionName = "Example BDC",
                    LicenceType = "BDC"
                }
            ]
        };

        var sut = new DashboardBriefingPackBuilder();

        var sections = sut.Build(workspace, "executive", 11, null, null, null);

        sections.Should().HaveCount(6);
        sections.Should().ContainSingle(x => x.SectionCode == "EXE-06")
            .Which.Should().BeEquivalentTo(new
            {
                SectionName = "Data & Intelligence Freshness",
                Coverage = "No freshness telemetry",
                Signal = "Watch"
            });
    }
}
