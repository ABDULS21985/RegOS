using System.Text.Json;
using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Portal.Services;

/// <summary>
/// Manages institution profile, team members, and portal settings.
/// Provides full CRUD for institution admins and read-only for other users.
/// </summary>
public class InstitutionManagementService
{
    private readonly IInstitutionRepository _institutionRepo;
    private readonly IInstitutionUserRepository _userRepo;
    private readonly ISubmissionRepository _submissionRepo;
    private readonly InstitutionAuthService _authService;

    public InstitutionManagementService(
        IInstitutionRepository institutionRepo,
        IInstitutionUserRepository userRepo,
        ISubmissionRepository submissionRepo,
        InstitutionAuthService authService)
    {
        _institutionRepo = institutionRepo;
        _userRepo = userRepo;
        _submissionRepo = submissionRepo;
        _authService = authService;
    }

    // ═══════════════════════════════════════════════════════════════
    //  INSTITUTION PROFILE
    // ═══════════════════════════════════════════════════════════════

    public async Task<InstitutionProfileModel?> GetProfile(int institutionId, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null) return null;

        var users = await _userRepo.GetByInstitution(institutionId, ct);
        var activeCount = users.Count(u => u.IsActive);

        int totalSubmissions = 0;
        DateTime? lastSubmissionDate = null;
        try
        {
            var submissions = await _submissionRepo.GetByInstitution(institutionId, ct);
            totalSubmissions = submissions.Count;
            lastSubmissionDate = submissions
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefault()?.SubmittedAt;
        }
        catch { /* Stats are non-critical */ }

        return new InstitutionProfileModel
        {
            Id = inst.Id,
            Code = inst.InstitutionCode,
            Name = inst.InstitutionName,
            LicenseType = inst.LicenseType ?? "",
            SubscriptionTier = inst.SubscriptionTier,
            Address = inst.Address ?? "",
            PhoneNumber = inst.ContactPhone ?? "",
            Email = inst.ContactEmail ?? "",
            IsActive = inst.IsActive,
            CreatedAt = inst.CreatedAt,
            MaxUsersAllowed = inst.MaxUsersAllowed,
            TotalUsers = users.Count,
            ActiveUsers = activeCount,
            TotalSubmissions = totalSubmissions,
            LastSubmissionDate = lastSubmissionDate,
            MakerCheckerEnabled = inst.MakerCheckerEnabled
        };
    }

    public async Task<bool> UpdateContactDetails(
        int institutionId, string email, string phone, string address, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null) return false;

        inst.ContactEmail = email;
        inst.ContactPhone = phone;
        inst.Address = address;

        await _institutionRepo.Update(inst, ct);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TEAM MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<TeamMemberDetailModel>> GetTeamMembers(int institutionId, CancellationToken ct = default)
    {
        var users = await _userRepo.GetByInstitution(institutionId, ct);

        return users.Select(u => new TeamMemberDetailModel
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Username = u.Username,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt,
            Initials = GetInitials(u.DisplayName)
        })
        .OrderBy(u => u.DisplayName)
        .ToList();
    }

    public async Task<(int current, int max)> GetUserCount(int institutionId, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        var count = await _userRepo.GetCountByInstitution(institutionId, ct);
        return (count, inst?.MaxUsersAllowed ?? 10);
    }

    public async Task<AddMemberResult> AddMember(
        int institutionId, string displayName, string username, string email,
        string role, string temporaryPassword, CancellationToken ct = default)
    {
        // Validate email uniqueness
        if (await _userRepo.EmailExists(email, ct))
            return new AddMemberResult { Success = false, Error = "A user with this email already exists." };

        // Validate username uniqueness
        if (await _userRepo.UsernameExists(username, ct))
            return new AddMemberResult { Success = false, Error = "This username is already taken." };

        // Check user limit
        var (current, max) = await GetUserCount(institutionId, ct);
        if (current >= max)
            return new AddMemberResult
            {
                Success = false,
                Error = $"User limit reached ({max} users). Contact CBN to increase your limit."
            };

        // Validate role
        if (!Enum.TryParse<InstitutionRole>(role, out var parsedRole))
            return new AddMemberResult { Success = false, Error = $"Invalid role: {role}" };

        // Delegate to InstitutionAuthService for proper password hashing
        try
        {
            var user = await _authService.CreateUser(
                institutionId, username, email, displayName, temporaryPassword, parsedRole, ct);

            return new AddMemberResult
            {
                Success = true,
                UserId = user.Id,
                Message = $"User {username} has been added successfully. They should log in with the temporary password and change it immediately."
            };
        }
        catch (InvalidOperationException ex)
        {
            return new AddMemberResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> UpdateMemberRole(int userId, string newRole, int institutionId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null || user.InstitutionId != institutionId) return false;

        if (!Enum.TryParse<InstitutionRole>(newRole, out var parsedRole)) return false;

        user.Role = parsedRole;
        await _userRepo.Update(user, ct);
        return true;
    }

    public async Task<bool> ToggleMemberStatus(int userId, bool isActive, int institutionId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null || user.InstitutionId != institutionId) return false;

        user.IsActive = isActive;
        await _userRepo.Update(user, ct);
        return true;
    }

    public async Task<bool> ResetMemberPassword(int userId, string newPassword, int institutionId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null || user.InstitutionId != institutionId) return false;

        return await _authService.ResetPassword(userId, newPassword, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    //  INSTITUTION SETTINGS
    // ═══════════════════════════════════════════════════════════════

    public async Task<InstitutionPortalSettings> GetSettings(int institutionId, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null) return new InstitutionPortalSettings();

        if (!string.IsNullOrEmpty(inst.SettingsJson))
        {
            try
            {
                return JsonSerializer.Deserialize<InstitutionPortalSettings>(inst.SettingsJson)
                       ?? new InstitutionPortalSettings();
            }
            catch { return new InstitutionPortalSettings(); }
        }
        return new InstitutionPortalSettings();
    }

    public async Task<bool> SaveSettings(int institutionId, InstitutionPortalSettings settings, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null) return false;

        inst.SettingsJson = JsonSerializer.Serialize(settings);
        await _institutionRepo.Update(inst, ct);
        return true;
    }

    public async Task<bool> SetMakerChecker(int institutionId, bool enabled, CancellationToken ct = default)
    {
        var inst = await _institutionRepo.GetById(institutionId, ct);
        if (inst is null) return false;

        inst.MakerCheckerEnabled = enabled;
        await _institutionRepo.Update(inst, ct);
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        return string.Concat(
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(w => w[0])
        ).ToUpper();
    }
}

// ── Data Models ──────────────────────────────────────────────────────

public class InstitutionProfileModel
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string LicenseType { get; set; } = "";
    public string SubscriptionTier { get; set; } = "";
    public string Address { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MaxUsersAllowed { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalSubmissions { get; set; }
    public DateTime? LastSubmissionDate { get; set; }
    public bool MakerCheckerEnabled { get; set; }
}

public class TeamMemberDetailModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Initials { get; set; } = "";
}

public class AddMemberResult
{
    public bool Success { get; set; }
    public int? UserId { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

public class InstitutionPortalSettings
{
    public bool EmailOnSubmissionResult { get; set; } = true;
    public bool EmailOnDeadlineApproaching { get; set; } = true;
    public int DeadlineReminderDays { get; set; } = 3;
    public string DefaultSubmissionFormat { get; set; } = "XmlUpload";
    public int SessionTimeoutHours { get; set; } = 4;
    public string TimezoneId { get; set; } = "Africa/Lagos";
}
