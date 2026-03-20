using System.Security.Claims;
using System.Text;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Metadata;
using FC.Engine.Domain.Models;
using FC.Engine.Domain.ValueObjects;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FC.Engine.Portal.TestHarness;

public static class BulkUploadHarnessServiceRegistration
{
    public static bool IsEnabled(IConfiguration configuration)
    {
        return string.Equals(configuration["FC_PORTAL_BULK_UPLOAD_HARNESS"], "1", StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AddBulkUploadHarness(this IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(x => x.ServiceType == typeof(IHostedService)
                        && x.ImplementationType?.Namespace?.StartsWith("FC.Engine.", StringComparison.Ordinal) == true)
                     .ToList())
        {
            services.Remove(descriptor);
        }

        var tenantContext = new HarnessTenantContext(HarnessDefaults.TenantId);
        var dbOptions = new DbContextOptionsBuilder<MetadataDbContext>()
            .UseInMemoryDatabase($"bulk-upload-harness-{Guid.NewGuid():N}")
            .Options;

        SeedMetadata(dbOptions, tenantContext);

        services.RemoveAll<ITenantContext>();
        services.AddSingleton<ITenantContext>(tenantContext);

        services.RemoveAll<DbContextOptions<MetadataDbContext>>();
        services.RemoveAll<MetadataDbContext>();
        services.RemoveAll<IDbContextFactory<MetadataDbContext>>();
        services.AddSingleton(dbOptions);
        services.AddScoped(_ => new MetadataDbContext(dbOptions, tenantContext));
        services.AddScoped<IDbContextFactory<MetadataDbContext>>(_ => new HarnessMetadataDbContextFactory(dbOptions, tenantContext));

        services.RemoveAll<AuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(_ => new HarnessAuthenticationStateProvider(CreatePrincipal()));

        services.RemoveAll<IEntitlementService>();
        services.AddScoped<IEntitlementService, HarnessEntitlementService>();

        services.RemoveAll<ITemplateDownloadService>();
        services.AddScoped<ITemplateDownloadService, HarnessTemplateDownloadService>();

        services.RemoveAll<IBulkUploadService>();
        services.AddScoped<IBulkUploadService, HarnessBulkUploadService>();

        services.RemoveAll<ITenantBrandingService>();
        services.AddScoped<ITenantBrandingService, HarnessTenantBrandingService>();

        services.RemoveAll<IConsentService>();
        services.AddScoped<IConsentService, HarnessConsentService>();

        services.RemoveAll<ISubscriptionService>();
        services.AddScoped<ISubscriptionService, HarnessSubscriptionService>();

        return services;
    }

    private static ClaimsPrincipal CreatePrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, HarnessDefaults.UserId.ToString()),
            new Claim(ClaimTypes.Name, "bulk.harness"),
            new Claim(ClaimTypes.Role, "Maker"),
            new Claim("DisplayName", "Bulk Harness"),
            new Claim("InstitutionId", HarnessDefaults.InstitutionId.ToString()),
            new Claim("InstitutionName", "Harness Institution"),
            new Claim("TenantId", HarnessDefaults.TenantId.ToString("D"))
        ], "Harness");

        return new ClaimsPrincipal(identity);
    }

    private static void SeedMetadata(DbContextOptions<MetadataDbContext> dbOptions, ITenantContext tenantContext)
    {
        using var db = new MetadataDbContext(dbOptions, tenantContext);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        db.Modules.Add(new Module
        {
            Id = HarnessDefaults.ModuleId,
            ModuleCode = "CAPITAL_SUPERVISION",
            ModuleName = "Capital Supervision",
            RegulatorCode = "CBN",
            DefaultFrequency = "Monthly",
            IsActive = true,
            SheetCount = 1,
            CreatedAt = DateTime.UtcNow
        });

        db.ReturnTemplates.Add(new ReturnTemplate
        {
            Id = HarnessDefaults.TemplateId,
            TenantId = HarnessDefaults.TenantId,
            ModuleId = HarnessDefaults.ModuleId,
            ReturnCode = HarnessDefaults.ReturnCode,
            Name = "Capital Buffer Register",
            Frequency = ReturnFrequency.Monthly,
            StructuralCategory = StructuralCategory.FixedRow,
            PhysicalTableName = "cap_buf",
            XmlRootElement = HarnessDefaults.ReturnCode,
            XmlNamespace = "urn:harness:cap-buf",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "harness",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "harness"
        });

        db.TemplateVersions.Add(new TemplateVersion
        {
            Id = HarnessDefaults.TemplateVersionId,
            TemplateId = HarnessDefaults.TemplateId,
            TenantId = HarnessDefaults.TenantId,
            VersionNumber = 1,
            Status = TemplateStatus.Published,
            EffectiveFrom = DateTime.UtcNow.Date,
            PublishedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = "harness",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "harness"
        });

        db.TemplateFields.Add(new TemplateField
        {
            Id = HarnessDefaults.TemplateFieldId,
            TemplateVersionId = HarnessDefaults.TemplateVersionId,
            FieldName = "amount",
            DisplayName = "Amount",
            FieldOrder = 1,
            DataType = FieldDataType.Decimal,
            SqlType = "decimal(18,2)",
            MinValue = "10",
            CreatedAt = DateTime.UtcNow
        });

        db.ReturnPeriods.Add(new ReturnPeriod
        {
            Id = HarnessDefaults.ReturnPeriodId,
            TenantId = HarnessDefaults.TenantId,
            ModuleId = HarnessDefaults.ModuleId,
            Year = 2026,
            Month = 3,
            Frequency = "Monthly",
            ReportingDate = new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
            DeadlineDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            IsOpen = true,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();
    }
}

internal static class HarnessDefaults
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const int InstitutionId = 41;
    public const int UserId = 4201;
    public const int ModuleId = 7;
    public const int TemplateId = 1001;
    public const int TemplateVersionId = 2001;
    public const int TemplateFieldId = 3001;
    public const int ReturnPeriodId = 202603;
    public const string ReturnCode = "CAP_BUF";
}

internal sealed class HarnessMetadataDbContextFactory : IDbContextFactory<MetadataDbContext>
{
    private readonly DbContextOptions<MetadataDbContext> _options;
    private readonly ITenantContext _tenantContext;

    public HarnessMetadataDbContextFactory(DbContextOptions<MetadataDbContext> options, ITenantContext tenantContext)
    {
        _options = options;
        _tenantContext = tenantContext;
    }

    public MetadataDbContext CreateDbContext() => new(_options, _tenantContext);

    public Task<MetadataDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

internal sealed class HarnessAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public HarnessAuthenticationStateProvider(ClaimsPrincipal principal)
    {
        _state = new AuthenticationState(principal);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);
}

internal sealed class HarnessTenantContext : ITenantContext
{
    public HarnessTenantContext(Guid tenantId)
    {
        CurrentTenantId = tenantId;
    }

    public Guid? CurrentTenantId { get; }
    public bool IsPlatformAdmin => false;
    public Guid? ImpersonatingTenantId => null;
}

internal sealed class HarnessEntitlementService : IEntitlementService
{
    public Task<TenantEntitlement> ResolveEntitlements(Guid tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(new TenantEntitlement
        {
            TenantId = tenantId,
            TenantStatus = TenantStatus.Active,
            ActiveModules =
            [
                new EntitledModule
                {
                    ModuleId = HarnessDefaults.ModuleId,
                    ModuleCode = "CAPITAL_SUPERVISION",
                    ModuleName = "Capital Supervision",
                    RegulatorCode = "CBN",
                    DefaultFrequency = "Monthly",
                    IsActive = true,
                    SheetCount = 1
                }
            ]
        });
    }

    public Task<bool> HasModuleAccess(Guid tenantId, string moduleCode, CancellationToken ct = default)
        => Task.FromResult(string.Equals(moduleCode, "CAPITAL_SUPERVISION", StringComparison.OrdinalIgnoreCase));

    public Task<bool> HasFeatureAccess(Guid tenantId, string featureCode, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task InvalidateCache(Guid tenantId) => Task.CompletedTask;
}

internal sealed class HarnessTemplateDownloadService : ITemplateDownloadService
{
    public Task<byte[]> GenerateTemplateExcel(Guid tenantId, string returnCode, CancellationToken ct = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<string> GenerateTemplateCsv(Guid tenantId, string returnCode, CancellationToken ct = default)
        => Task.FromResult("Amount\r\n");
}

internal sealed class HarnessBulkUploadService : IBulkUploadService
{
    public Task<BulkUploadResult> ProcessExcelUpload(
        Stream fileStream,
        Guid tenantId,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        int? requestedByUserId = null,
        CancellationToken ct = default)
    {
        return ProcessCsvUpload(fileStream, tenantId, returnCode, institutionId, returnPeriodId, requestedByUserId, ct);
    }

    public async Task<BulkUploadResult> ProcessCsvUpload(
        Stream fileStream,
        Guid tenantId,
        string returnCode,
        int institutionId,
        int returnPeriodId,
        int? requestedByUserId = null,
        CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync(ct);
        fileStream.Position = 0;

        if (!content.Contains("5", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Harness upload expected a failing CSV value of 5.");
        }

        return new BulkUploadResult
        {
            Success = false,
            SubmissionId = 501,
            Status = nameof(SubmissionStatus.Rejected),
            Message = "Upload validation failed.",
            Errors =
            [
                new BulkUploadError
                {
                    RowNumber = 2,
                    FieldCode = "amount",
                    Message = "'Amount' value 5 is below minimum 10",
                    Severity = "Error",
                    Category = BulkUploadErrorCategories.TypeRange,
                    ExpectedValue = ">= 10"
                }
            ]
        };
    }
}

internal sealed class HarnessTenantBrandingService : ITenantBrandingService
{
    public Task<BrandingConfig> GetBrandingConfig(Guid tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(BrandingConfig.WithDefaults(new BrandingConfig
        {
            CompanyName = "Harness Portal",
            FaviconUrl = "/favicon.svg",
            LogoSmallUrl = "/images/cbn-logo.svg"
        }));
    }

    public Task UpdateBrandingConfig(Guid tenantId, BrandingConfig config, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> UploadLogo(Guid tenantId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<string> UploadCompactLogo(Guid tenantId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<string> UploadFavicon(Guid tenantId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default) => throw new NotSupportedException();
    public Task InvalidateCache(Guid tenantId, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class HarnessConsentService : IConsentService
{
    public string GetCurrentPolicyVersion() => "test";
    public Task RecordConsent(ConsentCaptureRequest request, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> HasCurrentRequiredConsent(Guid tenantId, int userId, string userType, CancellationToken ct = default) => Task.FromResult(true);
    public Task<IReadOnlyList<ConsentRecord>> GetConsentHistory(Guid tenantId, int userId, string userType, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ConsentRecord>>(Array.Empty<ConsentRecord>());
    public Task WithdrawConsent(Guid tenantId, int userId, string userType, ConsentType consentType, string? ipAddress, string? userAgent, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class HarnessSubscriptionService : ISubscriptionService
{
    public Task<bool> HasFeature(Guid tenantId, string featureCode, CancellationToken ct = default) => Task.FromResult(false);

    public Task<Subscription> CreateSubscription(Guid tenantId, string planCode, BillingFrequency frequency, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Subscription> CreateSubscription(Guid tenantId, string planCode, BillingFrequency frequency, object? sharedDbContext, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Subscription> UpgradePlan(Guid tenantId, string newPlanCode, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Subscription> DowngradePlan(Guid tenantId, string newPlanCode, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CancelSubscription(Guid tenantId, string reason, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionModule> ActivateModule(Guid tenantId, string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionModule> ActivateModule(Guid tenantId, string moduleCode, object? sharedDbContext, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeactivateModule(Guid tenantId, string moduleCode, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<ModuleAvailability>> GetAvailableModules(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Invoice> GenerateInvoice(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Invoice> IssueInvoice(int invoiceId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Payment> RecordPayment(int invoiceId, RecordPaymentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task VoidInvoice(int invoiceId, string reason, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UsageSummary> GetUsageSummary(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> CheckLimit(Guid tenantId, string limitType, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Subscription> GetActiveSubscription(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Invoice>> GetInvoices(Guid tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> GetInvoiceCount(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<InvoiceStats> GetInvoiceStats(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<Payment>> GetPayments(Guid tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<int> GetPaymentCount(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<PaymentStats> GetPaymentStats(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<List<SubscriptionPlan>> GetAvailablePlans(Guid tenantId, CancellationToken ct = default) => throw new NotSupportedException();
}
