using FC.Engine.Domain.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FC.Engine.Infrastructure.Services;

/// <summary>
/// Publishes submission domain events via MassTransit.
/// In production this is backed by RabbitMQ; in dev/test it uses the in-memory transport.
/// Topic names mirror the Kafka convention specified in RG-34 for forward compatibility.
/// </summary>
public sealed class MassTransitSubmissionEventPublisher : ISubmissionEventPublisher
{
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<MassTransitSubmissionEventPublisher> _logger;

    public MassTransitSubmissionEventPublisher(
        IPublishEndpoint publish,
        ILogger<MassTransitSubmissionEventPublisher> logger)
    {
        _publish = publish;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topic, T payload, CancellationToken ct = default)
        where T : class
    {
        try
        {
            await _publish.Publish(payload, context =>
            {
                context.Headers.Set("submission-topic", topic);
            }, ct);

            _logger.LogDebug("Published submission event on topic '{Topic}': {PayloadType}",
                topic, typeof(T).Name);
        }
        catch (Exception ex)
        {
            // Event publication is best-effort; never block the submission pipeline
            _logger.LogWarning(ex, "Failed to publish submission event on topic '{Topic}'", topic);
        }
    }
}
