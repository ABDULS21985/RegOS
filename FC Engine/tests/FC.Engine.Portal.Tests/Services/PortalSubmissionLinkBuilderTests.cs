using FC.Engine.Portal.Services;
using FluentAssertions;
using Xunit;

namespace FC.Engine.Portal.Tests.Services;

public class PortalSubmissionLinkBuilderTests
{
    [Fact]
    public void BuildSubmitHref_Includes_Module_When_Module_Is_Known()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmitHref("CAP_BUF", "CAPITAL_SUPERVISION");

        href.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF");
    }

    [Fact]
    public void BuildSubmitHref_Falls_Back_To_Return_Code_Only_When_Module_Is_Missing()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmitHref("NFIU_TFS", null);

        href.Should().Be("/submit?returnCode=NFIU_TFS");
    }
}
