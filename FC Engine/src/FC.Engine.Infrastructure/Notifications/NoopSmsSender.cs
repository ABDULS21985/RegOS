using FC.Engine.Domain.Abstractions;

namespace FC.Engine.Infrastructure.Notifications;

public class NoopSmsSender : ISmsSender
{
    public Task<SmsSendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        return Task.FromResult(new SmsSendResult
        {
            Success = false,
            ErrorMessage = "No SMS provider configured."
        });
    }
}
