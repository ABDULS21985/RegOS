namespace FC.Engine.Domain.Entities;

public class InvoiceLineItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ModuleId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public int DisplayOrder { get; set; }

    public Invoice? Invoice { get; set; }
    public Module? Module { get; set; }
}
