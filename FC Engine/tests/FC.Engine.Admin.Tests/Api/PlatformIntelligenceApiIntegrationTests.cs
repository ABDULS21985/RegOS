using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FC.Engine.Admin.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FC.Engine.Admin.Tests.Api;

public sealed class PlatformIntelligenceApiIntegrationTests : IClassFixture<PlatformIntelligenceApiWebApplicationFactory>
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true
    };

    private readonly PlatformIntelligenceApiWebApplicationFactory _factory;

    public PlatformIntelligenceApiIntegrationTests(PlatformIntelligenceApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Overview_Requires_Authentication()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/intelligence/overview");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Overview_Returns_Workspace_Snapshot()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/overview");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "generatedAt", out var generatedAt).Should().BeTrue();
        generatedAt.ValueKind.Should().Be(JsonValueKind.String);
        TryGetProperty(json.RootElement, "hero", out _).Should().BeTrue();
        TryGetProperty(json.RootElement, "refresh", out _).Should().BeTrue();
        TryGetProperty(json.RootElement, "institutionCount", out var institutionCount).Should().BeTrue();
        TryGetProperty(json.RootElement, "interventionCount", out var interventionCount).Should().BeTrue();
        TryGetProperty(json.RootElement, "timelineCount", out var timelineCount).Should().BeTrue();
        institutionCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        interventionCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        timelineCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task KnowledgeCatalog_Returns_Persisted_Graph_State()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin");

        var response = await client.GetAsync("/api/intelligence/knowledge/catalog");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "nodeCount", out var nodeCount).Should().BeTrue();
        TryGetProperty(json.RootElement, "edgeCount", out var edgeCount).Should().BeTrue();
        TryGetProperty(json.RootElement, "nodeTypes", out var nodeTypes).Should().BeTrue();
        TryGetProperty(json.RootElement, "edgeTypes", out var edgeTypes).Should().BeTrue();
        nodeCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        edgeCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        nodeTypes.ValueKind.Should().Be(JsonValueKind.Array);
        edgeTypes.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task CapitalActionCatalog_Returns_Seeded_Templates()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin");

        var response = await client.GetAsync("/api/intelligence/capital/action-catalog");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "rows", out var rows).Should().BeTrue();
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        rows.GetArrayLength().Should().BeGreaterThan(0);
        rows.EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .Should()
            .Contain("COLLATERAL")
            .And.Contain("ISSUANCE");
    }

    [Fact]
    public async Task SanctionsCatalogSources_Returns_Baseline_Sources()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin");

        var response = await client.GetAsync("/api/intelligence/sanctions/catalog/sources");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "rows", out var rows).Should().BeTrue();
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        rows.GetArrayLength().Should().BeGreaterThan(0);
        var sourceCodes = rows.EnumerateArray()
            .Select(item => item.GetProperty("sourceCode").GetString())
            .ToList();
        sourceCodes.Should().Contain("UN");
        sourceCodes.Should().Contain("OFAC");
        sourceCodes.Should().Contain("NFIU");
    }

    [Fact]
    public async Task ModelRiskCatalog_Returns_Governed_Model_Definitions()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin");

        var response = await client.GetAsync("/api/intelligence/model-risk/catalog");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "rows", out var rows).Should().BeTrue();
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        rows.GetArrayLength().Should().BeGreaterThan(0);
        var modelCodes = rows.EnumerateArray()
            .Select(item => item.GetProperty("modelCode").GetString())
            .ToList();
        modelCodes.Should().Contain("ECL");
        modelCodes.Should().Contain("CAR");
        modelCodes.Should().Contain("STRESS");
    }

    [Fact]
    public async Task Rollout_Reconcile_Requires_Platform_Admin_Role()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin");

        var response = await client.PostAsJsonAsync(
            "/api/intelligence/rollout/reconcile",
            new { tenantIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_Csv_Export_Returns_Downloadable_File()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/overview/export.csv");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        GetDownloadFileName(response).Should().Be("platform-intelligence-overview.csv");
        payload.Should().Contain("Section,Metric,Value,Commentary");
        payload.Should().Contain("Workspace");
        payload.Should().Contain("Refresh");
    }

    [Fact]
    public async Task Overview_Pdf_Export_Returns_Downloadable_File()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/overview/export.pdf");
        var payload = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        GetDownloadFileName(response).Should().Be("platform-intelligence-board-brief.pdf");
        payload.Should().NotBeEmpty();
        Encoding.ASCII.GetString(payload.Take(5).ToArray()).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Export_Bundle_Zip_Contains_Manifest_And_Core_Files()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/export-bundle.zip?lens=governor");
        var payload = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
        GetDownloadFileName(response).Should().Be("platform-intelligence-bundle-governor.zip");

        using var stream = new MemoryStream(payload);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(x => x.FullName).ToList();
        entryNames.Should().Contain("manifest.json");
        entryNames.Should().Contain("platform-intelligence-overview.csv");
        entryNames.Should().Contain("platform-intelligence-board-brief.pdf");
        entryNames.Should().Contain("stakeholder-briefing-pack-governor.csv");
        entryNames.Should().Contain("stakeholder-briefing-pack-governor.pdf");
        entryNames.Should().Contain("knowledge-graph-dossier.csv");
        entryNames.Should().Contain("capital-supervisory-pack.csv");
        entryNames.Should().Contain("sanctions-supervisory-pack.csv");
        entryNames.Should().Contain("ops-resilience-pack.csv");
        entryNames.Should().Contain("model-risk-pack.csv");

        using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var manifest = await JsonDocument.ParseAsync(manifestStream, JsonOptions);
        TryGetProperty(manifest.RootElement, "lens", out var lens).Should().BeTrue();
        TryGetProperty(manifest.RootElement, "files", out var files).Should().BeTrue();
        lens.GetString().Should().Be("governor");
        files.ValueKind.Should().Be(JsonValueKind.Array);
        files.GetArrayLength().Should().BeGreaterThanOrEqualTo(9);
    }

    [Fact]
    public async Task Dashboard_Briefing_Pack_Csv_Export_Returns_Downloadable_File()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/dashboards/briefing-pack/export.csv?lens=governor");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        GetDownloadFileName(response).Should().Be("stakeholder-briefing-pack-governor.csv");
        payload.Should().Contain("Section Code,Section Name,Coverage,Signal,Commentary,Recommended Action,Materialized At");
    }

    [Fact]
    public async Task Dashboard_Briefing_Pack_Pdf_Export_Returns_Downloadable_File()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/dashboards/briefing-pack/export.pdf?lens=governor");
        var payload = await response.Content.ReadAsByteArrayAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        GetDownloadFileName(response).Should().Be("stakeholder-briefing-pack-governor.pdf");
        payload.Should().NotBeEmpty();
        Encoding.ASCII.GetString(payload.Take(5).ToArray()).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Dashboard_Briefing_Pack_Export_Rejects_Executive_Lens_Without_Institution()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/dashboards/briefing-pack/export.csv?lens=executive");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "error", out var error).Should().BeTrue();
        error.GetString().Should().Be("InstitutionId is required for the executive lens.");
    }

    [Theory]
    [InlineData("/api/intelligence/knowledge/dossier/export.csv", "knowledge-graph-dossier.csv", "Section Code,Section Name,Row Count,Signal,Coverage,Commentary,Recommended Action")]
    [InlineData("/api/intelligence/capital/pack/export.csv", "capital-supervisory-pack.csv", "Section Code,Section Name,Row Count,Signal,Coverage,Commentary,Recommended Action")]
    [InlineData("/api/intelligence/sanctions/pack/export.csv", "sanctions-supervisory-pack.csv", "Section Code,Section Name,Row Count,Signal,Coverage,Commentary,Recommended Action")]
    [InlineData("/api/intelligence/resilience/pack/export.csv", "ops-resilience-pack.csv", "Sheet Code,Sheet Name,Row Count,Signal,Coverage,Commentary,Recommended Action")]
    [InlineData("/api/intelligence/model-risk/pack/export.csv", "model-risk-pack.csv", "Sheet Code,Sheet Name,Row Count,Signal,Coverage,Commentary,Recommended Action")]
    public async Task Intelligence_Pack_Csv_Exports_Return_Downloadable_Files(
        string route,
        string expectedFileName,
        string expectedHeader)
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync(route);
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        GetDownloadFileName(response).Should().Be(expectedFileName);
        payload.Should().Contain(expectedHeader);
    }

    [Fact]
    public async Task Sanctions_Screening_Run_Returns_Matches_And_Persists_Latest_Session()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.PostAsJsonAsync(
            "/api/intelligence/sanctions/screen",
            new
            {
                subjects = new[] { "AL-QAIDA" },
                thresholdPercent = 86d
            });
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "totalSubjects", out var totalSubjects).Should().BeTrue();
        TryGetProperty(json.RootElement, "matchCount", out var matchCount).Should().BeTrue();
        TryGetProperty(json.RootElement, "results", out var results).Should().BeTrue();
        totalSubjects.GetInt32().Should().Be(1);
        matchCount.GetInt32().Should().BeGreaterThan(0);
        results.ValueKind.Should().Be(JsonValueKind.Array);
        results.GetArrayLength().Should().BeGreaterThan(0);

        var sessionResponse = await client.GetAsync("/api/intelligence/sanctions/session");
        var sessionPayload = await sessionResponse.Content.ReadAsStringAsync();

        sessionResponse.StatusCode.Should().Be(HttpStatusCode.OK, sessionPayload);
        using var sessionJson = JsonDocument.Parse(sessionPayload, JsonOptions);
        TryGetProperty(sessionJson.RootElement, "latestRun", out var latestRun).Should().BeTrue();
        latestRun.ValueKind.Should().Be(JsonValueKind.Object);
        TryGetProperty(latestRun, "totalSubjects", out var persistedTotalSubjects).Should().BeTrue();
        TryGetProperty(latestRun, "matchCount", out var persistedMatchCount).Should().BeTrue();
        TryGetProperty(latestRun, "results", out var persistedResults).Should().BeTrue();
        persistedTotalSubjects.GetInt32().Should().Be(1);
        persistedMatchCount.GetInt32().Should().Be(matchCount.GetInt32());
        persistedResults.EnumerateArray()
            .Select(x => x.GetProperty("matchedName").GetString())
            .Should()
            .Contain(name => string.Equals(name, "AL-QAIDA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sanctions_Workflow_Decision_Can_Be_Recorded_And_Read_Back()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");
        var matchKey = $"integration-match-{Guid.NewGuid():N}";

        var postResponse = await client.PostAsJsonAsync(
            "/api/intelligence/sanctions/workflow",
            new
            {
                matchKey,
                subject = "Integration Sanctions Subject",
                matchedName = "DOMESTIC AML WATCH SUBJECT",
                sourceCode = "NFIU",
                riskLevel = "high",
                previousDecision = "Review",
                decision = "False Positive"
            });
        var postPayload = await postResponse.Content.ReadAsStringAsync();

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK, postPayload);

        var getResponse = await client.GetAsync("/api/intelligence/sanctions/workflow");
        var getPayload = await getResponse.Content.ReadAsStringAsync();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getPayload);
        using var json = JsonDocument.Parse(getPayload, JsonOptions);
        TryGetProperty(json.RootElement, "latestDecisions", out var latestDecisions).Should().BeTrue();
        TryGetProperty(json.RootElement, "auditTrail", out var auditTrail).Should().BeTrue();
        TryGetProperty(json.RootElement, "falsePositiveLibrary", out var falsePositiveLibrary).Should().BeTrue();

        latestDecisions.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("matchKey").GetString(), matchKey, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("decision").GetString(), "False Positive", StringComparison.Ordinal));

        auditTrail.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("matchKey").GetString(), matchKey, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("previousDecision").GetString(), "Review", StringComparison.Ordinal)
                && string.Equals(item.GetProperty("decision").GetString(), "False Positive", StringComparison.Ordinal));

        falsePositiveLibrary.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("matchKey").GetString(), matchKey, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("sourceCode").GetString(), "NFIU", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Transaction_Screening_Can_Be_Recorded_And_Read_Back()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");
        var transactionReference = $"integration-tx-{Guid.NewGuid():N}";

        var postResponse = await client.PostAsJsonAsync(
            "/api/intelligence/sanctions/transactions/screen",
            new
            {
                transactionReference,
                amount = 1250000m,
                currency = "ngn",
                channel = "SWIFT",
                originatorName = "AL-QAIDA",
                beneficiaryName = "Integration Beneficiary",
                counterpartyName = "",
                highRisk = true
            });
        var postPayload = await postResponse.Content.ReadAsStringAsync();

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK, postPayload);
        using var resultJson = JsonDocument.Parse(postPayload, JsonOptions);
        TryGetProperty(resultJson.RootElement, "controlDecision", out var controlDecision).Should().BeTrue();
        TryGetProperty(resultJson.RootElement, "requiresStrDraft", out var requiresStrDraft).Should().BeTrue();
        TryGetProperty(resultJson.RootElement, "partyResults", out var partyResults).Should().BeTrue();
        controlDecision.GetString().Should().Be("Block");
        requiresStrDraft.GetBoolean().Should().BeTrue();
        partyResults.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("partyName").GetString(), "AL-QAIDA", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.GetProperty("sourceCode").GetString(), "UN", StringComparison.Ordinal));

        var sessionResponse = await client.GetAsync("/api/intelligence/sanctions/session");
        var sessionPayload = await sessionResponse.Content.ReadAsStringAsync();

        sessionResponse.StatusCode.Should().Be(HttpStatusCode.OK, sessionPayload);
        using var sessionJson = JsonDocument.Parse(sessionPayload, JsonOptions);
        TryGetProperty(sessionJson.RootElement, "latestTransaction", out var latestTransaction).Should().BeTrue();
        latestTransaction.ValueKind.Should().Be(JsonValueKind.Object);
        TryGetProperty(latestTransaction, "transactionReference", out var persistedReference).Should().BeTrue();
        TryGetProperty(latestTransaction, "controlDecision", out var persistedDecision).Should().BeTrue();
        TryGetProperty(latestTransaction, "requiresStrDraft", out var persistedRequiresStrDraft).Should().BeTrue();
        persistedReference.GetString().Should().Be(transactionReference);
        persistedDecision.GetString().Should().Be("Block");
        persistedRequiresStrDraft.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Resilience_Self_Assessment_Can_Be_Round_Tripped_And_Reset()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");
        var originalResponses = await LoadResilienceResponsesAsync(client);
        var questionId = $"integration-{Guid.NewGuid():N}";

        try
        {
            var postResponse = await client.PostAsJsonAsync(
                "/api/intelligence/resilience/self-assessment",
                new
                {
                    questionId,
                    domain = "Testing cadence",
                    prompt = "Integration coverage posture",
                    score = 4
                });
            var postPayload = await postResponse.Content.ReadAsStringAsync();

            postResponse.StatusCode.Should().Be(HttpStatusCode.OK, postPayload);

            var updatedResponses = await LoadResilienceResponsesAsync(client);
            updatedResponses.Should().ContainSingle(x =>
                x.QuestionId == questionId
                && x.Domain == "Testing cadence"
                && x.Prompt == "Integration coverage posture"
                && x.Score == 4);

            var resetResponse = await client.DeleteAsync("/api/intelligence/resilience/self-assessment");
            var resetPayload = await resetResponse.Content.ReadAsStringAsync();

            resetResponse.StatusCode.Should().Be(HttpStatusCode.OK, resetPayload);
            var responsesAfterReset = await LoadResilienceResponsesAsync(client);
            responsesAfterReset.Should().BeEmpty();
        }
        finally
        {
            var cleanupResponse = await client.DeleteAsync("/api/intelligence/resilience/self-assessment");
            cleanupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            foreach (var response in originalResponses)
            {
                var restoreResponse = await client.PostAsJsonAsync(
                    "/api/intelligence/resilience/self-assessment",
                    new
                    {
                        response.QuestionId,
                        response.Domain,
                        response.Prompt,
                        response.Score
                    });

                restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }
    }

    [Fact]
    public async Task Model_Risk_Approval_Workflow_Can_Be_Recorded_And_Read_Back()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");
        var workflowKey = $"integration-workflow-{Guid.NewGuid():N}";

        var postResponse = await client.PostAsJsonAsync(
            "/api/intelligence/model-risk/approval-workflow",
            new
            {
                workflowKey,
                modelCode = "INTTEST",
                modelName = "Integration Workflow Model",
                artifact = "integration-api-artifact",
                previousStage = "Model Owner",
                stage = "Validation Team"
            });
        var postPayload = await postResponse.Content.ReadAsStringAsync();

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK, postPayload);

        var getResponse = await client.GetAsync("/api/intelligence/model-risk/approval-workflow");
        var getPayload = await getResponse.Content.ReadAsStringAsync();

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getPayload);
        using var json = JsonDocument.Parse(getPayload, JsonOptions);
        TryGetProperty(json.RootElement, "stages", out var stages).Should().BeTrue();
        TryGetProperty(json.RootElement, "auditTrail", out var auditTrail).Should().BeTrue();

        stages.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("workflowKey").GetString(), workflowKey, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("modelCode").GetString(), "INTTEST", StringComparison.Ordinal)
                && string.Equals(item.GetProperty("stage").GetString(), "Validation Team", StringComparison.Ordinal));

        auditTrail.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("workflowKey").GetString(), workflowKey, StringComparison.Ordinal)
                && string.Equals(item.GetProperty("previousStage").GetString(), "Model Owner", StringComparison.Ordinal)
                && string.Equals(item.GetProperty("stage").GetString(), "Validation Team", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_Activity_Returns_Recent_Overview_Exports()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var exportResponse = await client.GetAsync("/api/intelligence/overview/export.csv");
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activityResponse = await client.GetAsync("/api/intelligence/exports/activity?area=Overview&format=csv&take=5");
        var payload = await activityResponse.Content.ReadAsStringAsync();

        activityResponse.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "total", out var total).Should().BeTrue();
        TryGetProperty(json.RootElement, "rows", out var rows).Should().BeTrue();
        total.GetInt32().Should().BeGreaterThan(0);
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        rows.EnumerateArray()
            .Should()
            .Contain(item =>
                string.Equals(item.GetProperty("area").GetString(), "Overview", StringComparison.Ordinal)
                && string.Equals(item.GetProperty("format").GetString(), "csv", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_Run_Returns_Result_And_Recent_Runs_Include_It()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var runResponse = await client.PostAsync("/api/intelligence/refresh/run", content: null);
        var runPayload = await runResponse.Content.ReadAsStringAsync();

        runResponse.StatusCode.Should().Be(HttpStatusCode.OK, runPayload);
        using var runJson = JsonDocument.Parse(runPayload, JsonOptions);
        TryGetProperty(runJson.RootElement, "generatedAt", out var generatedAt).Should().BeTrue();
        TryGetProperty(runJson.RootElement, "durationMilliseconds", out var durationMilliseconds).Should().BeTrue();
        TryGetProperty(runJson.RootElement, "dashboardPacksMaterialized", out var dashboardPacksMaterialized).Should().BeTrue();
        generatedAt.ValueKind.Should().Be(JsonValueKind.String);
        durationMilliseconds.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        dashboardPacksMaterialized.GetInt32().Should().BeGreaterThanOrEqualTo(0);

        var statusResponse = await client.GetAsync("/api/intelligence/refresh/status");
        var statusPayload = await statusResponse.Content.ReadAsStringAsync();

        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK, statusPayload);
        using var statusJson = JsonDocument.Parse(statusPayload, JsonOptions);
        TryGetProperty(statusJson.RootElement, "status", out var status).Should().BeTrue();
        TryGetProperty(statusJson.RootElement, "recentRuns", out var recentRuns).Should().BeTrue();
        status.GetString().Should().NotBeNullOrWhiteSpace();
        recentRuns.ValueKind.Should().Be(JsonValueKind.Array);
        recentRuns.GetArrayLength().Should().BeGreaterThan(0);

        var recentRunsResponse = await client.GetAsync("/api/intelligence/refresh/runs?take=5");
        var recentRunsPayload = await recentRunsResponse.Content.ReadAsStringAsync();

        recentRunsResponse.StatusCode.Should().Be(HttpStatusCode.OK, recentRunsPayload);
        using var recentRunsJson = JsonDocument.Parse(recentRunsPayload, JsonOptions);
        recentRunsJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        recentRunsJson.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        var hasWellFormedRun = recentRunsJson.RootElement.EnumerateArray()
            .Any(item =>
                item.TryGetProperty("generatedAtUtc", out _)
                && item.TryGetProperty("status", out var itemStatus)
                && !string.IsNullOrWhiteSpace(itemStatus.GetString()));
        hasWellFormedRun.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_Freshness_Can_Be_Filtered_By_Area()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/refresh/freshness?area=Knowledge&take=10");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "rows", out var rows).Should().BeTrue();
        TryGetProperty(json.RootElement, "total", out var total).Should().BeTrue();
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        total.GetInt32().Should().BeGreaterThanOrEqualTo(0);
        rows.EnumerateArray()
            .Should()
            .OnlyContain(item =>
                string.Equals(item.GetProperty("area").GetString(), "Knowledge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capital_Overview_And_Pack_Return_Snapshots()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var overviewResponse = await client.GetAsync("/api/intelligence/capital/overview");
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();

        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, overviewPayload);
        using var overviewJson = JsonDocument.Parse(overviewPayload, JsonOptions);
        TryGetProperty(overviewJson.RootElement, "returnPack", out var returnPack).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "returnPackAttentionCount", out var attentionCount).Should().BeTrue();
        returnPack.ValueKind.Should().Be(JsonValueKind.Array);
        attentionCount.GetInt32().Should().BeGreaterThanOrEqualTo(0);

        var packResponse = await client.GetAsync("/api/intelligence/capital/pack");
        var packPayload = await packResponse.Content.ReadAsStringAsync();

        packResponse.StatusCode.Should().Be(HttpStatusCode.OK, packPayload);
        using var packJson = JsonDocument.Parse(packPayload, JsonOptions);
        packJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Capital_Scenario_History_Returns_Array()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/capital/scenario/history?take=5");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Resilience_Overview_And_Pack_Return_Snapshots()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var overviewResponse = await client.GetAsync("/api/intelligence/resilience/overview");
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();

        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, overviewPayload);
        using var overviewJson = JsonDocument.Parse(overviewPayload, JsonOptions);
        TryGetProperty(overviewJson.RootElement, "returnPack", out var returnPack).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "recentIncidents", out var recentIncidents).Should().BeTrue();
        returnPack.ValueKind.Should().Be(JsonValueKind.Array);
        recentIncidents.ValueKind.Should().Be(JsonValueKind.Array);

        var packResponse = await client.GetAsync("/api/intelligence/resilience/pack");
        var packPayload = await packResponse.Content.ReadAsStringAsync();

        packResponse.StatusCode.Should().Be(HttpStatusCode.OK, packPayload);
        using var packJson = JsonDocument.Parse(packPayload, JsonOptions);
        packJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Rollout_Overview_And_Plan_Coverage_Return_Snapshots()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var overviewResponse = await client.GetAsync("/api/intelligence/rollout/overview");
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();

        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, overviewPayload);
        using var overviewJson = JsonDocument.Parse(overviewPayload, JsonOptions);
        TryGetProperty(overviewJson.RootElement, "planCoverage", out var planCoverage).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "reconciliationQueue", out var reconciliationQueue).Should().BeTrue();
        planCoverage.ValueKind.Should().Be(JsonValueKind.Array);
        reconciliationQueue.ValueKind.Should().Be(JsonValueKind.Array);

        var coverageResponse = await client.GetAsync("/api/intelligence/rollout/plan-coverage?take=5");
        var coveragePayload = await coverageResponse.Content.ReadAsStringAsync();

        coverageResponse.StatusCode.Should().Be(HttpStatusCode.OK, coveragePayload);
        using var coverageJson = JsonDocument.Parse(coveragePayload, JsonOptions);
        TryGetProperty(coverageJson.RootElement, "rows", out var coverageRows).Should().BeTrue();
        coverageRows.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Institution_Scorecards_Return_Catalog_State_And_Unknown_Detail_Is_Not_Found()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var scorecardsResponse = await client.GetAsync("/api/intelligence/institutions/scorecards?take=5");
        var scorecardsPayload = await scorecardsResponse.Content.ReadAsStringAsync();

        scorecardsResponse.StatusCode.Should().Be(HttpStatusCode.OK, scorecardsPayload);
        using var scorecardsJson = JsonDocument.Parse(scorecardsPayload, JsonOptions);
        TryGetProperty(scorecardsJson.RootElement, "rows", out var rows).Should().BeTrue();
        TryGetProperty(scorecardsJson.RootElement, "total", out var total).Should().BeTrue();
        rows.ValueKind.Should().Be(JsonValueKind.Array);
        total.GetInt32().Should().BeGreaterThanOrEqualTo(0);

        var detailResponse = await client.GetAsync("/api/intelligence/institutions/2147483647");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dashboard_Briefing_Pack_Returns_Governor_Sections()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var response = await client.GetAsync("/api/intelligence/dashboards/briefing-pack?lens=governor");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "sections", out var sections).Should().BeTrue();
        sections.ValueKind.Should().Be(JsonValueKind.Array);
        sections.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Overview_Operations_Endpoints_Return_Filterable_Rows()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var interventionsResponse = await client.GetAsync("/api/intelligence/overview/interventions?take=10");
        var interventionsPayload = await interventionsResponse.Content.ReadAsStringAsync();

        interventionsResponse.StatusCode.Should().Be(HttpStatusCode.OK, interventionsPayload);
        using var interventionsJson = JsonDocument.Parse(interventionsPayload, JsonOptions);
        TryGetProperty(interventionsJson.RootElement, "rows", out var interventionRows).Should().BeTrue();
        TryGetProperty(interventionsJson.RootElement, "total", out var interventionTotal).Should().BeTrue();
        interventionRows.ValueKind.Should().Be(JsonValueKind.Array);
        interventionTotal.GetInt32().Should().BeGreaterThanOrEqualTo(0);

        if (interventionRows.GetArrayLength() > 0)
        {
            var domain = interventionRows[0].GetProperty("domain").GetString();
            domain.Should().NotBeNullOrWhiteSpace();

            var filteredInterventionsResponse = await client.GetAsync(
                $"/api/intelligence/overview/interventions?domain={Uri.EscapeDataString(domain!)}&take=10");
            var filteredInterventionsPayload = await filteredInterventionsResponse.Content.ReadAsStringAsync();

            filteredInterventionsResponse.StatusCode.Should().Be(HttpStatusCode.OK, filteredInterventionsPayload);
            using var filteredInterventionsJson = JsonDocument.Parse(filteredInterventionsPayload, JsonOptions);
            filteredInterventionsJson.RootElement.GetProperty("rows").EnumerateArray()
                .Should()
                .OnlyContain(item =>
                    string.Equals(item.GetProperty("domain").GetString(), domain, StringComparison.OrdinalIgnoreCase));
        }

        var timelineResponse = await client.GetAsync("/api/intelligence/overview/timeline?take=10");
        var timelinePayload = await timelineResponse.Content.ReadAsStringAsync();

        timelineResponse.StatusCode.Should().Be(HttpStatusCode.OK, timelinePayload);
        using var timelineJson = JsonDocument.Parse(timelinePayload, JsonOptions);
        TryGetProperty(timelineJson.RootElement, "rows", out var timelineRows).Should().BeTrue();
        TryGetProperty(timelineJson.RootElement, "total", out var timelineTotal).Should().BeTrue();
        timelineRows.ValueKind.Should().Be(JsonValueKind.Array);
        timelineTotal.GetInt32().Should().BeGreaterThanOrEqualTo(0);

        if (timelineRows.GetArrayLength() > 0)
        {
            var severity = timelineRows[0].GetProperty("severity").GetString();
            severity.Should().NotBeNullOrWhiteSpace();

            var filteredTimelineResponse = await client.GetAsync(
                $"/api/intelligence/overview/timeline?severity={Uri.EscapeDataString(severity!)}&take=10");
            var filteredTimelinePayload = await filteredTimelineResponse.Content.ReadAsStringAsync();

            filteredTimelineResponse.StatusCode.Should().Be(HttpStatusCode.OK, filteredTimelinePayload);
            using var filteredTimelineJson = JsonDocument.Parse(filteredTimelinePayload, JsonOptions);
            filteredTimelineJson.RootElement.GetProperty("rows").EnumerateArray()
                .Should()
                .OnlyContain(item =>
                    string.Equals(item.GetProperty("severity").GetString(), severity, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Knowledge_EndPoints_Return_Obligations_Impact_Dossier_And_Navigator_Detail()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var obligationsResponse = await client.GetAsync("/api/intelligence/knowledge/obligations?take=10");
        var obligationsPayload = await obligationsResponse.Content.ReadAsStringAsync();

        obligationsResponse.StatusCode.Should().Be(HttpStatusCode.OK, obligationsPayload);
        using var obligationsJson = JsonDocument.Parse(obligationsPayload, JsonOptions);
        obligationsJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var impactResponse = await client.GetAsync("/api/intelligence/knowledge/impact-propagation?take=10");
        var impactPayload = await impactResponse.Content.ReadAsStringAsync();

        impactResponse.StatusCode.Should().Be(HttpStatusCode.OK, impactPayload);
        using var impactJson = JsonDocument.Parse(impactPayload, JsonOptions);
        impactJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var dossierResponse = await client.GetAsync("/api/intelligence/knowledge/dossier");
        var dossierPayload = await dossierResponse.Content.ReadAsStringAsync();

        dossierResponse.StatusCode.Should().Be(HttpStatusCode.OK, dossierPayload);
        using var dossierJson = JsonDocument.Parse(dossierPayload, JsonOptions);
        dossierJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        dossierJson.RootElement.GetArrayLength().Should().BeGreaterThan(0);

        if (impactJson.RootElement.GetArrayLength() > 0)
        {
            using var scope = _factory.Services.CreateScope();
            var intelligenceService = scope.ServiceProvider.GetRequiredService<FC.Engine.Admin.Services.PlatformIntelligenceService>();
            var workspace = await intelligenceService.GetWorkspaceAsync();
            var resolvedNavigatorKey = workspace.KnowledgeGraph.NavigatorDetails
                .Select(x => x.NavigatorKey)
                .FirstOrDefault();

            resolvedNavigatorKey.Should().NotBeNullOrWhiteSpace();

            var navigatorResponse = await client.GetAsync(
                $"/api/intelligence/knowledge/navigator?key={Uri.EscapeDataString(resolvedNavigatorKey!)}");
            var navigatorPayload = await navigatorResponse.Content.ReadAsStringAsync();

            navigatorResponse.StatusCode.Should().Be(HttpStatusCode.OK, navigatorPayload);
            using var navigatorJson = JsonDocument.Parse(navigatorPayload, JsonOptions);
            TryGetProperty(navigatorJson.RootElement, "navigatorKey", out var persistedKey).Should().BeTrue();
            TryGetProperty(navigatorJson.RootElement, "affectedInstitutions", out var affectedInstitutions).Should().BeTrue();
            TryGetProperty(navigatorJson.RootElement, "recentSubmissions", out var recentSubmissions).Should().BeTrue();
            persistedKey.GetString().Should().Be(resolvedNavigatorKey);
            affectedInstitutions.ValueKind.Should().Be(JsonValueKind.Array);
            recentSubmissions.ValueKind.Should().Be(JsonValueKind.Array);
        }
    }

    [Fact]
    public async Task Knowledge_Catalog_Nodes_And_Edges_Return_Filtered_State()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var nodesResponse = await client.GetAsync("/api/intelligence/knowledge/catalog/nodes?take=10");
        var nodesPayload = await nodesResponse.Content.ReadAsStringAsync();

        nodesResponse.StatusCode.Should().Be(HttpStatusCode.OK, nodesPayload);
        using var nodesJson = JsonDocument.Parse(nodesPayload, JsonOptions);
        TryGetProperty(nodesJson.RootElement, "rows", out var nodeRows).Should().BeTrue();
        nodeRows.ValueKind.Should().Be(JsonValueKind.Array);

        if (nodeRows.GetArrayLength() > 0)
        {
            var nodeType = nodeRows[0].GetProperty("nodeType").GetString();
            nodeType.Should().NotBeNullOrWhiteSpace();

            var filteredNodesResponse = await client.GetAsync(
                $"/api/intelligence/knowledge/catalog/nodes?nodeType={Uri.EscapeDataString(nodeType!)}&take=10");
            var filteredNodesPayload = await filteredNodesResponse.Content.ReadAsStringAsync();

            filteredNodesResponse.StatusCode.Should().Be(HttpStatusCode.OK, filteredNodesPayload);
            using var filteredNodesJson = JsonDocument.Parse(filteredNodesPayload, JsonOptions);
            filteredNodesJson.RootElement.GetProperty("rows").EnumerateArray()
                .Should()
                .OnlyContain(item =>
                    string.Equals(item.GetProperty("nodeType").GetString(), nodeType, StringComparison.OrdinalIgnoreCase));
        }

        var edgesResponse = await client.GetAsync("/api/intelligence/knowledge/catalog/edges?take=10");
        var edgesPayload = await edgesResponse.Content.ReadAsStringAsync();

        edgesResponse.StatusCode.Should().Be(HttpStatusCode.OK, edgesPayload);
        using var edgesJson = JsonDocument.Parse(edgesPayload, JsonOptions);
        TryGetProperty(edgesJson.RootElement, "rows", out var edgeRows).Should().BeTrue();
        edgeRows.ValueKind.Should().Be(JsonValueKind.Array);

        if (edgeRows.GetArrayLength() > 0)
        {
            var edgeType = edgeRows[0].GetProperty("edgeType").GetString();
            edgeType.Should().NotBeNullOrWhiteSpace();

            var filteredEdgesResponse = await client.GetAsync(
                $"/api/intelligence/knowledge/catalog/edges?edgeType={Uri.EscapeDataString(edgeType!)}&take=10");
            var filteredEdgesPayload = await filteredEdgesResponse.Content.ReadAsStringAsync();

            filteredEdgesResponse.StatusCode.Should().Be(HttpStatusCode.OK, filteredEdgesPayload);
            using var filteredEdgesJson = JsonDocument.Parse(filteredEdgesPayload, JsonOptions);
            filteredEdgesJson.RootElement.GetProperty("rows").EnumerateArray()
                .Should()
                .OnlyContain(item =>
                    string.Equals(item.GetProperty("edgeType").GetString(), edgeType, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Sanctions_Read_Endpoints_Return_Snapshots_And_Persisted_State()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var overviewResponse = await client.GetAsync("/api/intelligence/sanctions/overview");
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();

        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, overviewPayload);
        using var overviewJson = JsonDocument.Parse(overviewPayload, JsonOptions);
        TryGetProperty(overviewJson.RootElement, "returnPack", out var returnPack).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "sources", out var sources).Should().BeTrue();
        returnPack.ValueKind.Should().Be(JsonValueKind.Array);
        sources.ValueKind.Should().Be(JsonValueKind.Array);

        var packResponse = await client.GetAsync("/api/intelligence/sanctions/pack");
        var packPayload = await packResponse.Content.ReadAsStringAsync();

        packResponse.StatusCode.Should().Be(HttpStatusCode.OK, packPayload);
        using var packJson = JsonDocument.Parse(packPayload, JsonOptions);
        packJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var entriesResponse = await client.GetAsync("/api/intelligence/sanctions/catalog/entries?take=10");
        var entriesPayload = await entriesResponse.Content.ReadAsStringAsync();

        entriesResponse.StatusCode.Should().Be(HttpStatusCode.OK, entriesPayload);
        using var entriesJson = JsonDocument.Parse(entriesPayload, JsonOptions);
        TryGetProperty(entriesJson.RootElement, "rows", out var entryRows).Should().BeTrue();
        entryRows.ValueKind.Should().Be(JsonValueKind.Array);

        if (entryRows.GetArrayLength() > 0)
        {
            var sourceCode = entryRows[0].GetProperty("sourceCode").GetString();
            sourceCode.Should().NotBeNullOrWhiteSpace();

            var filteredEntriesResponse = await client.GetAsync(
                $"/api/intelligence/sanctions/catalog/entries?sourceCode={Uri.EscapeDataString(sourceCode!)}&take=10");
            var filteredEntriesPayload = await filteredEntriesResponse.Content.ReadAsStringAsync();

            filteredEntriesResponse.StatusCode.Should().Be(HttpStatusCode.OK, filteredEntriesPayload);
            using var filteredEntriesJson = JsonDocument.Parse(filteredEntriesPayload, JsonOptions);
            filteredEntriesJson.RootElement.GetProperty("rows").EnumerateArray()
                .Should()
                .OnlyContain(item =>
                    string.Equals(item.GetProperty("sourceCode").GetString(), sourceCode, StringComparison.OrdinalIgnoreCase));
        }

        var strDraftsResponse = await client.GetAsync("/api/intelligence/sanctions/str-drafts");
        var strDraftsPayload = await strDraftsResponse.Content.ReadAsStringAsync();

        strDraftsResponse.StatusCode.Should().Be(HttpStatusCode.OK, strDraftsPayload);
        using var strDraftsJson = JsonDocument.Parse(strDraftsPayload, JsonOptions);
        TryGetProperty(strDraftsJson.RootElement, "drafts", out var drafts).Should().BeTrue();
        drafts.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Model_Risk_Read_Endpoints_Return_Snapshots_And_Registers()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");

        var overviewResponse = await client.GetAsync("/api/intelligence/model-risk/overview");
        var overviewPayload = await overviewResponse.Content.ReadAsStringAsync();

        overviewResponse.StatusCode.Should().Be(HttpStatusCode.OK, overviewPayload);
        using var overviewJson = JsonDocument.Parse(overviewPayload, JsonOptions);
        TryGetProperty(overviewJson.RootElement, "inventory", out var inventory).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "approvalQueue", out var approvalQueue).Should().BeTrue();
        TryGetProperty(overviewJson.RootElement, "returnPack", out var returnPack).Should().BeTrue();
        inventory.ValueKind.Should().Be(JsonValueKind.Array);
        approvalQueue.ValueKind.Should().Be(JsonValueKind.Array);
        returnPack.ValueKind.Should().Be(JsonValueKind.Array);

        var packResponse = await client.GetAsync("/api/intelligence/model-risk/pack");
        var packPayload = await packResponse.Content.ReadAsStringAsync();

        packResponse.StatusCode.Should().Be(HttpStatusCode.OK, packPayload);
        using var packJson = JsonDocument.Parse(packPayload, JsonOptions);
        packJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var inventoryResponse = await client.GetAsync("/api/intelligence/model-risk/inventory?take=10");
        var inventoryPayload = await inventoryResponse.Content.ReadAsStringAsync();

        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK, inventoryPayload);
        using var inventoryJson = JsonDocument.Parse(inventoryPayload, JsonOptions);
        inventoryJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var backtestingResponse = await client.GetAsync("/api/intelligence/model-risk/backtesting?take=10");
        var backtestingPayload = await backtestingResponse.Content.ReadAsStringAsync();

        backtestingResponse.StatusCode.Should().Be(HttpStatusCode.OK, backtestingPayload);
        using var backtestingJson = JsonDocument.Parse(backtestingPayload, JsonOptions);
        backtestingJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var monitoringResponse = await client.GetAsync("/api/intelligence/model-risk/monitoring?take=10");
        var monitoringPayload = await monitoringResponse.Content.ReadAsStringAsync();

        monitoringResponse.StatusCode.Should().Be(HttpStatusCode.OK, monitoringPayload);
        using var monitoringJson = JsonDocument.Parse(monitoringPayload, JsonOptions);
        monitoringJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        var approvalQueueResponse = await client.GetAsync("/api/intelligence/model-risk/approval-queue");
        var approvalQueuePayload = await approvalQueueResponse.Content.ReadAsStringAsync();

        approvalQueueResponse.StatusCode.Should().Be(HttpStatusCode.OK, approvalQueuePayload);
        using var approvalQueueJson = JsonDocument.Parse(approvalQueuePayload, JsonOptions);
        approvalQueueJson.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Capital_Scenario_Can_Be_Saved_Read_Back_And_Restored()
    {
        using var client = _factory.CreateAuthenticatedClient("Admin", "PlatformAdmin");
        var originalScenario = await LoadCapitalScenarioAsync(client);

        var request = new
        {
            currentCarPercent = 18.4m,
            currentRwaBn = 640m,
            quarterlyRwaGrowthPercent = 2.5m,
            quarterlyRetainedEarningsBn = 8.2m,
            capitalActionBn = 30m,
            minimumRequirementPercent = 10m,
            conservationBufferPercent = 2.5m,
            countercyclicalBufferPercent = 1m,
            dsibBufferPercent = 1.5m,
            rwaOptimisationPercent = 4m,
            targetCarPercent = 20m,
            cet1CostPercent = 12m,
            at1CostPercent = 16m,
            tier2CostPercent = 14m,
            maxAt1SharePercent = 25m,
            maxTier2SharePercent = 25m,
            stepPercent = 0.5m
        };

        try
        {
            var postResponse = await client.PostAsJsonAsync("/api/intelligence/capital/scenario", request);
            var postPayload = await postResponse.Content.ReadAsStringAsync();

            postResponse.StatusCode.Should().Be(HttpStatusCode.OK, postPayload);

            var getResponse = await client.GetAsync("/api/intelligence/capital/scenario");
            var getPayload = await getResponse.Content.ReadAsStringAsync();

            getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getPayload);
            using var getJson = JsonDocument.Parse(getPayload, JsonOptions);
            TryGetProperty(getJson.RootElement, "currentRwaBn", out var currentRwaBn).Should().BeTrue();
            TryGetProperty(getJson.RootElement, "capitalActionBn", out var capitalActionBn).Should().BeTrue();
            TryGetProperty(getJson.RootElement, "targetCarPercent", out var targetCarPercent).Should().BeTrue();
            currentRwaBn.GetDecimal().Should().Be(640m);
            capitalActionBn.GetDecimal().Should().Be(30m);
            targetCarPercent.GetDecimal().Should().Be(20m);
        }
        finally
        {
            if (originalScenario is not null)
            {
                var restoreResponse = await client.PostAsJsonAsync("/api/intelligence/capital/scenario", new
                {
                    originalScenario.CurrentCarPercent,
                    originalScenario.CurrentRwaBn,
                    originalScenario.QuarterlyRwaGrowthPercent,
                    originalScenario.QuarterlyRetainedEarningsBn,
                    originalScenario.CapitalActionBn,
                    originalScenario.MinimumRequirementPercent,
                    originalScenario.ConservationBufferPercent,
                    originalScenario.CountercyclicalBufferPercent,
                    DsibBufferPercent = originalScenario.DsibBufferPercent,
                    originalScenario.RwaOptimisationPercent,
                    originalScenario.TargetCarPercent,
                    originalScenario.Cet1CostPercent,
                    originalScenario.At1CostPercent,
                    originalScenario.Tier2CostPercent,
                    originalScenario.MaxAt1SharePercent,
                    originalScenario.MaxTier2SharePercent,
                    originalScenario.StepPercent
                });

                restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        return element.TryGetProperty(name, out value)
               || element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value);
    }

    private static string? GetDownloadFileName(HttpResponseMessage response)
    {
        return response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"')
               ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
    }

    private static async Task<List<ResilienceResponseDto>> LoadResilienceResponsesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/intelligence/resilience/self-assessment");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);
        using var json = JsonDocument.Parse(payload, JsonOptions);
        TryGetProperty(json.RootElement, "responses", out var responses).Should().BeTrue();
        responses.ValueKind.Should().Be(JsonValueKind.Array);

        return responses.EnumerateArray()
            .Select(item => new ResilienceResponseDto(
                item.GetProperty("questionId").GetString() ?? string.Empty,
                item.GetProperty("domain").GetString() ?? string.Empty,
                item.GetProperty("prompt").GetString() ?? string.Empty,
                item.GetProperty("score").GetInt32()))
            .ToList();
    }

    private static async Task<CapitalScenarioDto?> LoadCapitalScenarioAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/intelligence/capital/scenario");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, payload);

        using var json = JsonDocument.Parse(payload, JsonOptions);
        return new CapitalScenarioDto(
            json.RootElement.GetProperty("currentCarPercent").GetDecimal(),
            json.RootElement.GetProperty("currentRwaBn").GetDecimal(),
            json.RootElement.GetProperty("quarterlyRwaGrowthPercent").GetDecimal(),
            json.RootElement.GetProperty("quarterlyRetainedEarningsBn").GetDecimal(),
            json.RootElement.GetProperty("capitalActionBn").GetDecimal(),
            json.RootElement.GetProperty("minimumRequirementPercent").GetDecimal(),
            json.RootElement.GetProperty("conservationBufferPercent").GetDecimal(),
            json.RootElement.GetProperty("countercyclicalBufferPercent").GetDecimal(),
            json.RootElement.GetProperty("dsibBufferPercent").GetDecimal(),
            json.RootElement.GetProperty("rwaOptimisationPercent").GetDecimal(),
            json.RootElement.GetProperty("targetCarPercent").GetDecimal(),
            json.RootElement.GetProperty("cet1CostPercent").GetDecimal(),
            json.RootElement.GetProperty("at1CostPercent").GetDecimal(),
            json.RootElement.GetProperty("tier2CostPercent").GetDecimal(),
            json.RootElement.GetProperty("maxAt1SharePercent").GetDecimal(),
            json.RootElement.GetProperty("maxTier2SharePercent").GetDecimal(),
            json.RootElement.GetProperty("stepPercent").GetDecimal());
    }

    private sealed record ResilienceResponseDto(
        string QuestionId,
        string Domain,
        string Prompt,
        int Score);

    private sealed record CapitalScenarioDto(
        decimal CurrentCarPercent,
        decimal CurrentRwaBn,
        decimal QuarterlyRwaGrowthPercent,
        decimal QuarterlyRetainedEarningsBn,
        decimal CapitalActionBn,
        decimal MinimumRequirementPercent,
        decimal ConservationBufferPercent,
        decimal CountercyclicalBufferPercent,
        decimal DsibBufferPercent,
        decimal RwaOptimisationPercent,
        decimal TargetCarPercent,
        decimal Cet1CostPercent,
        decimal At1CostPercent,
        decimal Tier2CostPercent,
        decimal MaxAt1SharePercent,
        decimal MaxTier2SharePercent,
        decimal StepPercent);
}
