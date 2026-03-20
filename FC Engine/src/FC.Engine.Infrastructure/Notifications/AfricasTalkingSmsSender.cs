using System.Net.Http.Headers;
using System.Text.Json;
using FC.Engine.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace FC.Engine.Infrastructure.Notifications;

public class AfricasTalkingSmsSender : ISmsSender
{
    private readonly HttpClient _httpClient;
    private readonly AfricasTalkingSettings _settings;

    public AfricasTalkingSmsSender(HttpClient httpClient, IOptions<NotificationSettings> notificationOptions)
    {
        _httpClient = httpClient;
        _settings = notificationOptions.Value.Sms.AfricasTalking;
    }

    public async Task<SmsSendResult> SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.Username))
        {
            return new SmsSendResult
            {
                Success = false,
                ErrorMessage = "Africa's Talking settings are not configured."
            };
        }

        var normalizedPhone = NormalizeNigerianPhone(phoneNumber);
        if (message.Length > 160)
        {
            message = message[..157] + "...";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/version1/messaging");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apiKey", _settings.ApiKey);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _settings.Username,
            ["to"] = normalizedPhone,
            ["message"] = message,
            ["from"] = _settings.SenderId ?? "RegOS"
        });

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new SmsSendResult
            {
                Success = false,
                ErrorMessage = body
            };
        }

        return ParseResponse(body);
    }

    internal static string NormalizeNigerianPhone(string phone)
    {
        var normalized = (phone ?? string.Empty)
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);

        if (normalized.StartsWith("0", StringComparison.Ordinal))
        {
            normalized = "+234" + normalized[1..];
        }
        else if (normalized.StartsWith("234", StringComparison.Ordinal))
        {
            normalized = "+" + normalized;
        }
        else if (!normalized.StartsWith("+", StringComparison.Ordinal))
        {
            normalized = "+" + normalized;
        }

        return normalized;
    }

    private static SmsSendResult ParseResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var data = root.GetProperty("SMSMessageData");
            var recipients = data.GetProperty("Recipients");

            if (recipients.GetArrayLength() == 0)
            {
                return new SmsSendResult
                {
                    Success = true
                };
            }

            var recipient = recipients[0];
            var status = recipient.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;

            var costText = recipient.TryGetProperty("cost", out var costElement)
                ? costElement.GetString()
                : null;

            return new SmsSendResult
            {
                Success = status.Contains("Success", StringComparison.OrdinalIgnoreCase),
                ProviderMessageId = recipient.TryGetProperty("messageId", out var msgId)
                    ? msgId.GetString()
                    : null,
                ErrorMessage = status,
                Cost = TryParseCost(costText)
            };
        }
        catch (Exception ex)
        {
            return new SmsSendResult
            {
                Success = false,
                ErrorMessage = $"Unable to parse Africa's Talking response: {ex.Message}"
            };
        }
    }

    private static decimal? TryParseCost(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var numeric = parts.Length > 1 ? parts[1] : parts[0];
        if (decimal.TryParse(numeric, out var value))
        {
            return value;
        }

        return null;
    }
}
