using FC.Engine.Domain.Enums;

namespace FC.Engine.Domain.Entities;

public class PartnerSupportTicket
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerTenantId { get; set; }
    public int RaisedByUserId { get; set; }
    public string RaisedByUserName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PartnerSupportTicketPriority Priority { get; set; } = PartnerSupportTicketPriority.Normal;
    public PartnerSupportTicketStatus Status { get; set; } = PartnerSupportTicketStatus.Open;
    public int EscalationLevel { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public int? EscalatedByUserId { get; set; }
    public DateTime SlaDueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
