namespace FC.Engine.Domain.Entities;

public class LoginAttempt
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public DateTime AttemptedAt { get; set; }
}
