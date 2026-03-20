namespace FC.Engine.Domain.Entities;

public class ModuleVersion
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeprecatedAt { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Module? Module { get; set; }
}
