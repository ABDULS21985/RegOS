namespace FC.Engine.Domain.Metadata;

public class TemplateItemCode
{
    public int Id { get; set; }
    public int TemplateVersionId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsTotalRow { get; set; }
    public DateTime CreatedAt { get; set; }

    public TemplateItemCode Clone()
    {
        return new TemplateItemCode
        {
            ItemCode = ItemCode,
            ItemDescription = ItemDescription,
            SortOrder = SortOrder,
            IsTotalRow = IsTotalRow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
