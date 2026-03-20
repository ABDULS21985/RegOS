using System.Text;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace FC.Engine.Integration.Tests.PortalUi;

[CollectionDefinition("BulkUploadUiHarness", DisableParallelization = true)]
public sealed class BulkUploadUiHarnessCollection : ICollectionFixture<BulkUploadHarnessWebApplicationFactory>;

[Collection("BulkUploadUiHarness")]
public sealed class BulkUploadUiHarnessTests : IClassFixture<BulkUploadHarnessWebApplicationFactory>, IAsyncLifetime
{
    private readonly BulkUploadHarnessWebApplicationFactory _factory;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public BulkUploadUiHarnessTests(BulkUploadHarnessWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    [Fact]
    public async Task BulkUploadHarness_Renders_ExpectedValue_In_FixModal_And_Csv_Export()
    {
        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(new Uri(_factory.RootUri, "/__harness/bulk-upload").ToString());

        await page.GetByRole(AriaRole.Heading, new() { Name = "Bulk Upload" }).WaitForAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#templateSelect')?.value === 'CAP_BUF'");

        var upload = page.Locator("#bulk-dropzone input[type=file]");
        await upload.SetInputFilesAsync(
        [
            new FilePayload
            {
                Name = "cap_buf_invalid.csv",
                MimeType = "text/csv",
                Buffer = Encoding.UTF8.GetBytes("Amount\n5\n")
            }
        ]);

        await page.GetByText("cap_buf_invalid.csv").WaitForAsync();
        await page.WaitForFunctionAsync(
            "() => { const button = [...document.querySelectorAll('button')].find(x => x.textContent?.includes('Submit All')); return button && !button.disabled; }");

        await page.GetByRole(AriaRole.Button, new() { Name = "Submit All" }).ClickAsync();

        var fixButton = page.GetByRole(AriaRole.Button, new() { Name = "Fix & Re-upload" });
        await fixButton.WaitForAsync();
        await fixButton.ClickAsync();

        var expectedCell = page.Locator(".portal-bulk-fix-expected").First;
        await expectedCell.WaitForAsync();
        (await expectedCell.InnerTextAsync()).Should().Be(">= 10");

        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Button, new() { Name = "Download Error Report (CSV)" }).ClickAsync());

        var csvPath = Path.Combine(Path.GetTempPath(), $"bulk-upload-errors-{Guid.NewGuid():N}.csv");
        await download.SaveAsAsync(csvPath);

        var csv = await File.ReadAllTextAsync(csvPath);
        csv.Should().Contain("ExpectedFormat");
        csv.Should().Contain(">= 10");
    }
}
