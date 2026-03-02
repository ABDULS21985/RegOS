namespace FC.Engine.Domain.Metadata;

public class TemplateSection
{
    public int Id { get; set; }
    public int TemplateVersionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public int SectionOrder { get; set; }
    public string? Description { get; set; }
    public bool IsRepeating { get; set; }
}
