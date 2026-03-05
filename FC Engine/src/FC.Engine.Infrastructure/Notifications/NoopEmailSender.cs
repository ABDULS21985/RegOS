using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.ValueObjects;

namespace FC.Engine.Infrastructure.Notifications;

public class NoopEmailSender : IEmailSender
{
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        return Task.FromResult(new EmailSendResult
        {
            Success = false,
            ErrorMessage = "No email provider configured."
        });
    }

    public Task<EmailSendResult> SendTemplatedAsync(
        string templateId,
        Dictionary<string, string> variables,
        string toEmail,
        string toName,
        BrandingConfig branding,
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new EmailSendResult
        {
            Success = false,
            ErrorMessage = "No email provider configured."
        });
    }
}
