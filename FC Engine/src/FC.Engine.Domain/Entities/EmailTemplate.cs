namespace FC.Engine.Domain.Entities;

public class EmailTemplate
{
    public int Id { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public Guid? TenantId { get; set; } // null = system default
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? Variables { get; set; } // JSON array of variables
    public bool IsActive { get; set; } = true;
}
