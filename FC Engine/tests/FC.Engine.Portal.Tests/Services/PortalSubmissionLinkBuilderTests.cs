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

    [Fact]
    public void BuildSubmitHref_Uses_Module_Only_When_Return_Code_Is_Not_Known()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmitHref(null, "OPS_RESILIENCE");

        href.Should().Be("/submit?module=OPS_RESILIENCE");
    }

    [Fact]
    public void BuildSubmitHref_Includes_Period_When_Period_Is_Provided()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmitHref("CAP_BUF", "CAPITAL_SUPERVISION", 243);

        href.Should().Be("/submit?module=CAPITAL_SUPERVISION&returnCode=CAP_BUF&periodId=243");
    }

    [Fact]
    public void BuildBulkSubmitHref_Uses_Module_And_Return_Context_When_Available()
    {
        var href = PortalSubmissionLinkBuilder.BuildBulkSubmitHref("MODEL_RISK", "MRM_INV");

        href.Should().Be("/submit/bulk?module=MODEL_RISK&returnCode=MRM_INV");
    }

    [Fact]
    public void BuildBulkSubmitHref_Falls_Back_To_Generic_Bulk_Upload_When_Context_Is_Missing()
    {
        var href = PortalSubmissionLinkBuilder.BuildBulkSubmitHref(null);

        href.Should().Be("/submit/bulk");
    }

    [Fact]
    public void BuildSubmissionListHref_Uses_Module_Context_When_Available()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmissionListHref("OPS_RESILIENCE");

        href.Should().Be("/submissions?module=OPS_RESILIENCE");
    }

    [Fact]
    public void BuildSubmissionListHref_Falls_Back_To_Generic_Submissions_List_When_Module_Is_Missing()
    {
        var href = PortalSubmissionLinkBuilder.BuildSubmissionListHref(null);

        href.Should().Be("/submissions");
    }

    [Fact]
    public void BuildCalendarHref_Uses_Module_Return_And_Extension_Context_When_Available()
    {
        var href = PortalSubmissionLinkBuilder.BuildCalendarHref("MODEL_RISK", "MRM_INV", requestExtension: true);

        href.Should().Be("/calendar?module=MODEL_RISK&returnCode=MRM_INV&requestExtension=true");
    }

    [Fact]
    public void BuildCalendarHref_Falls_Back_To_Generic_Calendar_When_Context_Is_Missing()
    {
        var href = PortalSubmissionLinkBuilder.BuildCalendarHref();

        href.Should().Be("/calendar");
    }
}
