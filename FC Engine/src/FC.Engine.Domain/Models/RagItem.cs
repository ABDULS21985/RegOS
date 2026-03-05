namespace FC.Engine.Domain.Models;

/// <summary>
/// RAG (Red/Amber/Green) status item for a module's filing period.
/// </summary>
public class RagItem
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public RagColor Color { get; set; }

    public string CssClass => Color switch
    {
        RagColor.Green => "rag-green",
        RagColor.Amber => "rag-amber",
        RagColor.Red => "rag-red",
        _ => "rag-green"
    };
}

public enum RagColor
{
    Green,
    Amber,
    Red
}
