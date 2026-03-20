namespace FC.Engine.Domain.Abstractions;

/// <summary>
/// Publishes domain events for the regulatory submission pipeline.
/// Backed by MassTransit (RabbitMQ in prod, in-memory in dev/test).
/// Topic naming mirrors the Kafka topic convention from the spec.
/// </summary>
public interface ISubmissionEventPublisher
{
    Task PublishAsync<T>(string topic, T payload, CancellationToken ct = default)
        where T : class;
}
