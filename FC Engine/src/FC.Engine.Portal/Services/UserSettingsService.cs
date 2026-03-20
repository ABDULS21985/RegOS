using FC.Engine.Application.Services;
using FC.Engine.Domain.Abstractions;

namespace FC.Engine.Portal.Services;

/// <summary>
/// Provides user profile, institution details, and team roster
/// data for the FI Portal settings page.
/// </summary>
public class UserSettingsService
{
    private readonly IInstitutionUserRepository _userRepo;
    private readonly InstitutionAuthService _authService;

    public UserSettingsService(
        IInstitutionUserRepository userRepo,
        InstitutionAuthService authService)
    {
        _userRepo = userRepo;
        _authService = authService;
    }

    /// <summary>
    /// Load the current user's profile.
    /// </summary>
    public async Task<UserProfileModel?> GetUserProfile(int userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null) return null;

        return new UserProfileModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PreferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "en" : user.PreferredLanguage,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            InstitutionId = user.InstitutionId
        };
    }

    /// <summary>
    /// Update the current user's display name.
    /// Users cannot change their email, username, or role — those are managed by CBN admins.
    /// </summary>
    public async Task<bool> UpdateProfile(int userId, string displayName, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null) return false;

        user.DisplayName = displayName.Trim();
        await _userRepo.Update(user, ct);
        return true;
    }

    public async Task<bool> UpdatePreferredLanguage(int userId, string languageCode, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        if (user is null)
        {
            return false;
        }

        user.PreferredLanguage = string.IsNullOrWhiteSpace(languageCode)
            ? "en"
            : languageCode.Trim().ToLowerInvariant();
        await _userRepo.Update(user, ct);
        return true;
    }

    /// <summary>
    /// Change the user's password via InstitutionAuthService.
    /// Returns a specific error message on failure so the UI can distinguish causes.
    /// </summary>
    public async Task<(bool Success, string? Error)> ChangePassword(
        int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            return (false, "Password must be at least 8 characters.");

        if (!newPassword.Any(char.IsUpper))
            return (false, "Password must include at least one uppercase letter.");

        if (!newPassword.Any(char.IsLower))
            return (false, "Password must include at least one lowercase letter.");

        if (!newPassword.Any(char.IsDigit))
            return (false, "Password must include at least one number.");

        var success = await _authService.ChangePassword(userId, currentPassword, newPassword, ct);

        if (!success)
            return (false, "The current password is incorrect.");

        return (true, null);
    }

    /// <summary>
    /// Load institution details via the user's Institution navigation property.
    /// </summary>
    public async Task<InstitutionDetailModel?> GetInstitutionDetail(int userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetById(userId, ct);
        var inst = user?.Institution;
        if (inst is null) return null;

        return new InstitutionDetailModel
        {
            Id = inst.Id,
            Code = inst.InstitutionCode,
            Name = inst.InstitutionName,
            LicenseType = inst.LicenseType ?? "",
            ContactEmail = inst.ContactEmail ?? "",
            ContactPhone = inst.ContactPhone ?? "",
            Address = inst.Address ?? "",
            SubscriptionTier = inst.SubscriptionTier,
            MaxUsersAllowed = inst.MaxUsersAllowed,
            MakerCheckerEnabled = inst.MakerCheckerEnabled,
            IsActive = inst.IsActive,
            CreatedAt = inst.CreatedAt,
            LastSubmissionAt = inst.LastSubmissionAt
        };
    }

    /// <summary>
    /// Load all users for the institution (team roster).
    /// Only accessible to Admin-role users.
    /// </summary>
    public async Task<List<TeamMemberModel>> GetTeamRoster(int institutionId, CancellationToken ct = default)
    {
        var users = await _userRepo.GetByInstitution(institutionId, ct);

        return users.Select(u => new TeamMemberModel
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Username = u.Username,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            LastLoginAt = u.LastLoginAt,
            CreatedAt = u.CreatedAt
        })
        .OrderBy(u => u.DisplayName)
        .ToList();
    }
}

// ── Data Models ──────────────────────────────────────────────────────

public class UserProfileModel
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int InstitutionId { get; set; }

    public string Initials =>
        string.IsNullOrEmpty(DisplayName)
            ? "?"
            : string.Concat(
                DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0])
              ).ToUpper();
}

public class InstitutionDetailModel
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string LicenseType { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string Address { get; set; } = "";
    public string SubscriptionTier { get; set; } = "";
    public int MaxUsersAllowed { get; set; }
    public bool MakerCheckerEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSubmissionAt { get; set; }
}

public class TeamMemberModel
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public string Initials =>
        string.IsNullOrEmpty(DisplayName)
            ? "?"
            : string.Concat(
                DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)
                    .Select(w => w[0])
              ).ToUpper();
}
