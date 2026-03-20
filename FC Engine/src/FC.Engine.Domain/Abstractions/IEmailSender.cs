using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Domain.Abstractions;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default);

    Task<EmailSendResult> SendTemplatedAsync(
        string templateId,
        Dictionary<string, string> variables,
        string toEmail,
        string toName,
        BrandingConfig branding,
        Guid? tenantId = null,
        CancellationToken ct = default);
}

public class EmailMessage
{
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public string? ReplyTo { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class EmailSendResult
{
    public bool Success { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
}
