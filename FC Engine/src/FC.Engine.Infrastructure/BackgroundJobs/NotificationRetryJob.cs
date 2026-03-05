using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Entities;
using FC.Engine.Domain.Enums;
using FC.Engine.Domain.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.BackgroundJobs;

public class NotificationRetryJob : BackgroundService
{
    private readonly INotificationDeliveryRepository _deliveryRepository;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly ITenantBrandingService _brandingService;
    private readonly ILogger<NotificationRetryJob> _logger;

    public NotificationRetryJob(
        INotificationDeliveryRepository deliveryRepository,
        IEmailSender emailSender,
        ISmsSender smsSender,
        ITenantBrandingService brandingService,
        ILogger<NotificationRetryJob> logger)
    {
        _deliveryRepository = deliveryRepository;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _brandingService = brandingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RetryFailedDeliveries(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification retry cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task RetryFailedDeliveries(CancellationToken ct)
    {
        var failedDeliveries = await _deliveryRepository.GetRetryBatch(50, ct);
        if (failedDeliveries.Count == 0)
        {
            return;
        }

        foreach (var delivery in failedDeliveries)
        {
            await RetryDelivery(delivery, ct);
        }
    }

    private async Task RetryDelivery(NotificationDelivery delivery, CancellationToken ct)
    {
        try
        {
            var variables = DeserializePayload(delivery.Payload);
            var success = false;
            string? providerMessageId = null;
            string? error = null;

            if (delivery.Channel == NotificationChannel.Email)
            {
                var branding = await _brandingService.GetBrandingConfig(delivery.TenantId, ct);
                var emailResult = await _emailSender.SendTemplatedAsync(
                    delivery.NotificationEventType,
                    variables,
                    delivery.RecipientAddress,
                    variables.TryGetValue("RecipientName", out var recipientName) ? recipientName : string.Empty,
                    branding,
                    delivery.TenantId,
                    ct);

                success = emailResult.Success;
                providerMessageId = emailResult.ProviderMessageId;
                error = emailResult.ErrorMessage;
            }
            else if (delivery.Channel == NotificationChannel.Sms)
            {
                var sms = BuildSmsText(delivery.NotificationEventType, variables);
                if (sms.Length > 160)
                {
                    sms = sms[..157] + "...";
                }

                var smsResult = await _smsSender.SendAsync(delivery.RecipientAddress, sms, ct);
                success = smsResult.Success;
                providerMessageId = smsResult.ProviderMessageId;
                error = smsResult.ErrorMessage;
            }

            delivery.AttemptCount++;
            delivery.ProviderMessageId = providerMessageId;
            delivery.ErrorMessage = error;

            if (success)
            {
                delivery.Status = DeliveryStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;
                delivery.NextRetryAt = null;
            }
            else
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.NextRetryAt = DateTime.UtcNow.Add(GetRetryDelay(delivery.AttemptCount));
            }

            await _deliveryRepository.Update(delivery, ct);
        }
        catch (Exception ex)
        {
            delivery.AttemptCount++;
            delivery.Status = DeliveryStatus.Failed;
            delivery.ErrorMessage = ex.Message;
            delivery.NextRetryAt = DateTime.UtcNow.Add(GetRetryDelay(delivery.AttemptCount));
            await _deliveryRepository.Update(delivery, ct);
            _logger.LogError(ex, "Notification retry failed for delivery {DeliveryId}", delivery.Id);
        }
    }

    private static Dictionary<string, string> DeserializePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(raw)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 1 => TimeSpan.FromMinutes(5),
            2 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(2)
        };
    }

    private static string BuildSmsText(string eventType, IReadOnlyDictionary<string, string> vars)
    {
        static string Get(IReadOnlyDictionary<string, string> map, string key, string fallback = "")
            => map.TryGetValue(key, out var value) ? value : fallback;

        return eventType switch
        {
            NotificationEvents.MfaCodeSms => $"Your RegOS verification code is {Get(vars, "Code")}. Valid for 5 minutes. Do not share.",
            NotificationEvents.DeadlineT1 =>
                $"URGENT: {Get(vars, "ModuleName", "Return")} due TOMORROW {Get(vars, "Deadline")}. Login: regos.app",
            NotificationEvents.DeadlineOverdue =>
                $"OVERDUE: {Get(vars, "ModuleName", "Return")} past deadline. Submit immediately. regos.app",
            NotificationEvents.PaymentOverdue =>
                $"RegOS invoice {Get(vars, "InvoiceNumber")} overdue. Pay now to avoid suspension.",
            _ => Get(vars, "Message", "You have a new RegOS notification.")
        };
    }
}
