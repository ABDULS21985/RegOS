using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class PortalUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public PortalRole Role { get; set; } = PortalRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
