namespace FC.Engine.Domain.Entities;

public class OpsResiliencePackSheetRecord
{
    public int Id { get; set; }
    public string SheetCode { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string Signal { get; set; } = string.Empty;
    public string Coverage { get; set; } = string.Empty;
    public string Commentary { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public DateTime MaterializedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
