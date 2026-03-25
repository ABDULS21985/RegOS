using System.Collections.ObjectModel;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OtpNet;

namespace FC.Engine.Migrator;

public sealed class DemoCredentialSeedService
{
    private const string PlatformLoginUrl = "http://localhost:5200/login";
    private const string InstitutionLoginUrl = "http://localhost:5300/login";

    private static readonly DemoPortalUserSpec[] PlatformUsers =
    [
        new("admin", "System Administrator", "admin@fcengine.local", PortalRole.Admin, requiresMfa: false),
        new("platformapprover", "Platform Approver", "platform.approver@fcengine.local", PortalRole.Approver, requiresMfa: true),
        new("platformviewer", "Platform Viewer", "platform.viewer@fcengine.local", PortalRole.Viewer, requiresMfa: false)
    ];

    private static readonly DemoInstitutionUserSpec[] BdcUsers =
    [
        new("cashcodeadmin", "Cashcode Admin", "admin@cashcode.local", InstitutionRole.Admin, requiresMfa: false),
        new("cashcodemaker", "Cashcode Maker", "maker@cashcode.local", InstitutionRole.Maker, requiresMfa: false),
        new("cashcodechecker", "Cashcode Checker", "checker@cashcode.local", InstitutionRole.Checker, requiresMfa: true),
        new("cashcodeviewer", "Cashcode Viewer", "viewer@cashcode.local", InstitutionRole.Viewer, requiresMfa: false),
        new("cashcodeapprover", "Cashcode Approver", "approver@cashcode.local", InstitutionRole.Approver, requiresMfa: true)
    ];

    private static readonly DemoInstitutionUserSpec[] DmbUsers =
    [
        new("accessdemo", "Access Demo Admin", "accessdemo@accessbank.local", InstitutionRole.Admin, requiresMfa: false),
        new("accessmaker", "Access Demo Maker", "maker@accessbank.local", InstitutionRole.Maker, requiresMfa: false),
        new("accesschecker", "Access Demo Checker", "checker@accessbank.local", InstitutionRole.Checker, requiresMfa: true),
        new("accessviewer", "Access Demo Viewer", "viewer@accessbank.local", InstitutionRole.Viewer, requiresMfa: false),
        new("accessapprover", "Access Demo Approver", "approver@accessbank.local", InstitutionRole.Approver, requiresMfa: true)
    ];

    private readonly MetadataDbContext _db;
    private readonly AuthService _authService;
    private readonly InstitutionAuthService _institutionAuthService;
    private readonly IPortalUserRepository _portalUserRepository;
    private readonly IInstitutionUserRepository _institutionUserRepository;
    private readonly IMfaService _mfaService;
    private readonly ILogger<DemoCredentialSeedService> _logger;

    public DemoCredentialSeedService(
        MetadataDbContext db,
        AuthService authService,
        InstitutionAuthService institutionAuthService,
        IPortalUserRepository portalUserRepository,
        IInstitutionUserRepository institutionUserRepository,
        IMfaService mfaService,
        ILogger<DemoCredentialSeedService> logger)
    {
        _db = db;
        _authService = authService;
        _institutionAuthService = institutionAuthService;
        _portalUserRepository = portalUserRepository;
        _institutionUserRepository = institutionUserRepository;
        _mfaService = mfaService;
        _logger = logger;
    }

    public async Task<DemoCredentialSeedResult> SeedAsync(string sharedPassword, CancellationToken ct = default)
    {
        var result = new DemoCredentialSeedResult
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            SharedPassword = sharedPassword
        };

        foreach (var spec in PlatformUsers)
        {
            result.PlatformAccounts.Add(await EnsurePortalUserAsync(spec, sharedPassword, ct));
        }

        var bdcInstitution = await ResolveInstitutionAsync("CASHCODE", "CASHCODE BDC LTD", ct);
        result.InstitutionGroups.Add(await EnsureInstitutionUsersAsync(
            bdcInstitution,
            "BDC demo tenant with live BDC_CBN templates and samples.",
            BdcUsers,
            sharedPassword,
            ct));

        var dmbInstitution = await ResolveInstitutionAsync("ACCESSBA", "Access Bank Plc", ct);
        result.InstitutionGroups.Add(await EnsureInstitutionUsersAsync(
            dmbInstitution,
            "DMB demo tenant with seeded DMB_BASEL3 history and zero-warning DMB_OPR sample.",
            DmbUsers,
            sharedPassword,
            ct));

        _logger.LogInformation(
            "Seeded demo credential matrix: {PlatformCount} platform accounts, {InstitutionCount} institution accounts",
            result.PlatformAccounts.Count,
            result.InstitutionGroups.Sum(x => x.Accounts.Count));

        return result;
    }

    private async Task<DemoCredentialAccount> EnsurePortalUserAsync(
        DemoPortalUserSpec spec,
        string password,
        CancellationToken ct)
    {
        var user = await _portalUserRepository.GetByUsername(spec.Username, ct);
        if (user is null)
        {
            user = await _authService.CreateUser(
                spec.Username,
                spec.DisplayName,
                spec.Email,
                password,
                spec.Role,
                tenantId: null,
                ct);
        }
        else
        {
            await _authService.ChangePassword(user.Id, password, ct);
            user = await _portalUserRepository.GetByUsername(spec.Username, ct)
                   ?? throw new InvalidOperationException($"Portal user {spec.Username} disappeared after password reset.");
        }

        user.TenantId = null;
        user.DisplayName = spec.DisplayName;
        user.Email = spec.Email;
        user.Role = spec.Role;
        user.IsActive = true;
        user.DeletedAt = null;
        user.DeletionReason = null;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _portalUserRepository.Update(user, ct);

        var mfa = await ConfigureMfaAsync(user.Id, "PortalUser", user.Email, spec.RequiresMfa, ct);

        return new DemoCredentialAccount
        {
            Audience = "Platform",
            LoginUrl = PlatformLoginUrl,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            Password = password,
            MfaRequired = spec.RequiresMfa,
            TotpSecret = mfa?.TotpSecret,
            BackupCodes = mfa?.BackupCodes ?? []
        };
    }

    private async Task<DemoCredentialGroup> EnsureInstitutionUsersAsync(
        Institution institution,
        string notes,
        IReadOnlyList<DemoInstitutionUserSpec> specs,
        string password,
        CancellationToken ct)
    {
        var group = new DemoCredentialGroup
        {
            Audience = institution.LicenseType ?? "Institution",
            LoginUrl = InstitutionLoginUrl,
            InstitutionCode = institution.InstitutionCode,
            InstitutionName = institution.InstitutionName,
            LicenseType = institution.LicenseType ?? "Unknown",
            Notes = notes
        };

        foreach (var spec in specs)
        {
            group.Accounts.Add(await EnsureInstitutionUserAsync(institution, spec, password, ct));
        }

        return group;
    }

    private async Task<DemoCredentialAccount> EnsureInstitutionUserAsync(
        Institution institution,
        DemoInstitutionUserSpec spec,
        string password,
        CancellationToken ct)
    {
        var user = await _institutionUserRepository.GetByUsername(spec.Username, ct);
        if (user is null)
        {
            user = await _institutionAuthService.CreateUser(
                institution.Id,
                spec.Username,
                spec.Email,
                spec.DisplayName,
                password,
                spec.Role,
                ct);
        }
        else
        {
            if (user.InstitutionId != institution.Id)
            {
                throw new InvalidOperationException(
                    $"Institution user {spec.Username} already belongs to institution {user.InstitutionId}, expected {institution.Id}.");
            }

            await _institutionAuthService.ResetPassword(user.Id, password, ct);
            user = await _institutionUserRepository.GetById(user.Id, ct)
                   ?? throw new InvalidOperationException($"Institution user {spec.Username} disappeared after password reset.");
        }

        user.TenantId = institution.TenantId;
        user.InstitutionId = institution.Id;
        user.DisplayName = spec.DisplayName;
        user.Email = spec.Email;
        user.Role = spec.Role;
        user.PermissionOverridesJson = null;
        user.IsActive = true;
        user.MustChangePassword = false;
        user.PreferredLanguage = "en";
        user.DeletedAt = null;
        user.DeletionReason = null;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _institutionUserRepository.Update(user, ct);

        var mfa = await ConfigureMfaAsync(user.Id, "InstitutionUser", user.Email, spec.RequiresMfa, ct);

        return new DemoCredentialAccount
        {
            Audience = institution.InstitutionCode,
            LoginUrl = InstitutionLoginUrl,
            InstitutionCode = institution.InstitutionCode,
            InstitutionName = institution.InstitutionName,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            Password = password,
            MfaRequired = spec.RequiresMfa,
            TotpSecret = mfa?.TotpSecret,
            BackupCodes = mfa?.BackupCodes ?? []
        };
    }

    private async Task<DemoMfaMaterial?> ConfigureMfaAsync(
        int userId,
        string userType,
        string email,
        bool required,
        CancellationToken ct)
    {
        if (!required)
        {
            await _mfaService.Disable(userId, userType);
            return null;
        }

        var setup = await _mfaService.InitiateSetup(userId, userType, email);
        var code = new Totp(Base32Encoding.ToBytes(setup.SecretKey)).ComputeTotp(DateTime.UtcNow);
        var activation = await _mfaService.ActivateWithVerification(userId, userType, code);
        if (!activation.Success)
        {
            throw new InvalidOperationException($"Unable to activate MFA for {userType}:{userId}.");
        }

        return new DemoMfaMaterial(setup.SecretKey, new ReadOnlyCollection<string>(activation.BackupCodes));
    }

    private async Task<Institution> ResolveInstitutionAsync(string institutionCode, string institutionName, CancellationToken ct)
    {
        var institution = await _db.Institutions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.InstitutionCode == institutionCode || x.InstitutionName == institutionName,
                ct);

        return institution ?? throw new InvalidOperationException(
            $"Institution {institutionCode} / {institutionName} was not found.");
    }
}

public sealed class DemoCredentialSeedResult
{
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string SharedPassword { get; set; } = string.Empty;
    public List<DemoCredentialAccount> PlatformAccounts { get; } = [];
    public List<DemoCredentialGroup> InstitutionGroups { get; } = [];
}

public sealed class DemoCredentialGroup
{
    public string Audience { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string InstitutionCode { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<DemoCredentialAccount> Accounts { get; } = [];
}

public sealed class DemoCredentialAccount
{
    public string Audience { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string InstitutionCode { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool MfaRequired { get; set; }
    public string? TotpSecret { get; set; }
    public IReadOnlyList<string> BackupCodes { get; set; } = [];
}

file sealed record DemoPortalUserSpec(
    string Username,
    string DisplayName,
    string Email,
    PortalRole Role,
    bool RequiresMfa);

file sealed record DemoInstitutionUserSpec(
    string Username,
    string DisplayName,
    string Email,
    InstitutionRole Role,
    bool RequiresMfa);

file sealed record DemoMfaMaterial(
    string TotpSecret,
    IReadOnlyList<string> BackupCodes);
