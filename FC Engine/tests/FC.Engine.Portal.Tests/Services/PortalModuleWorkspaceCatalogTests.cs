using FC.Engine.Portal.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class PortalModuleWorkspaceCatalogTests
{
    [Theory]
    [InlineData("CAPITAL_SUPERVISION", "CAPITAL_SUPERVISION", "capital-supervision")]
    [InlineData("capital-supervision", "CAPITAL_SUPERVISION", "capital-supervision")]
    [InlineData("OPS_RESILIENCE", "OPS_RESILIENCE", "ops-resilience")]
    [InlineData("model-risk", "MODEL_RISK", "model-risk")]
    public void TryGetDefinition_Resolves_Module_Code_And_Slug(string key, string expectedModuleCode, string expectedSlug)
    {
        var found = PortalModuleWorkspaceCatalog.TryGetDefinition(key, out var definition);

        found.Should().BeTrue();
        definition.ModuleCode.Should().Be(expectedModuleCode);
        definition.Slug.Should().Be(expectedSlug);
        PortalModuleWorkspaceCatalog.ResolveModuleCode(key).Should().Be(expectedModuleCode);
        PortalModuleWorkspaceCatalog.GetWorkspaceHref(expectedModuleCode).Should().Be($"/workflows/{expectedSlug}");
    }

    [Fact]
    public void HasWorkspace_Returns_False_For_Unknown_Module()
    {
        PortalModuleWorkspaceCatalog.HasWorkspace("UNKNOWN_MODULE").Should().BeFalse();
        PortalModuleWorkspaceCatalog.ResolveModuleCode("UNKNOWN_MODULE").Should().BeNull();
    }
}
