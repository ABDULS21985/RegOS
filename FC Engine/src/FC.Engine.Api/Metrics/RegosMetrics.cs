using Prometheus;

namespace FC.Engine.Api.Metrics;

public static class RegosMetrics
{
    public static readonly Counter SubmissionsProcessed = Prometheus.Metrics.CreateCounter(
        "regos_submissions_processed_total",
        "Total submissions processed",
        new CounterConfiguration { LabelNames = new[] { "tenant", "module", "status" } });

    public static readonly Counter ExportsGenerated = Prometheus.Metrics.CreateCounter(
        "regos_exports_generated_total",
        "Total exports generated",
        new CounterConfiguration { LabelNames = new[] { "format" } });

    public static readonly Histogram ValidationDuration = Prometheus.Metrics.CreateHistogram(
        "regos_validation_duration_seconds",
        "Validation pipeline duration",
        new HistogramConfiguration { LabelNames = new[] { "module", "phase" } });

    public static readonly Gauge ActiveTenants = Prometheus.Metrics.CreateGauge(
        "regos_active_tenants",
        "Number of active tenants");

    public static readonly Counter RateLimitHits = Prometheus.Metrics.CreateCounter(
        "regos_rate_limit_hits_total",
        "Requests rejected by rate limiting",
        new CounterConfiguration { LabelNames = new[] { "tenant_id", "plan_code" } });
}
