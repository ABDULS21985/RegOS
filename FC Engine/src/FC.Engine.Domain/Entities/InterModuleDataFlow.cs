namespace FC.Engine.Domain.Entities;

public class InterModuleDataFlow
{
    public int Id { get; set; }
    public int SourceModuleId { get; set; }
    public string SourceTemplateCode { get; set; } = string.Empty;
    public string SourceFieldCode { get; set; } = string.Empty;
    public string TargetModuleCode { get; set; } = string.Empty;
    public string TargetTemplateCode { get; set; } = string.Empty;
    public string TargetFieldCode { get; set; } = string.Empty;
    public string TransformationType { get; set; } = "DirectCopy";
    public string? TransformFormula { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Module? SourceModule { get; set; }
    public Module? TargetModule { get; set; }
}
