namespace FC.Engine.Domain.Entities;

public class ResilienceAssessmentResponseRecord
{
    public int Id { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime AnsweredAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
