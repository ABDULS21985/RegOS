namespace FC.Engine.Domain.Abstractions;

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

public class SmsSendResult
{
    public bool Success { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal? Cost { get; set; }
}
