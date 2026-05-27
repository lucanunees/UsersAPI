using Prometheus;

namespace UsersAPI.Web.Metrics;

public static class AppMetrics
{
    // ─── Métricas de Cache ─────────────────────────────────
    public static readonly Counter CacheHits = Prometheus.Metrics.CreateCounter(
        "cache_hits_total",
        "Total de cache hits",
        new CounterConfiguration { LabelNames = new[] { "endpoint" } });

    public static readonly Counter CacheMisses = Prometheus.Metrics.CreateCounter(
        "cache_misses_total",
        "Total de cache misses",
        new CounterConfiguration { LabelNames = new[] { "endpoint" } });

    // ─── Métricas de Negócio ───────────────────────────────
    public static readonly Counter UsersRegistered = Prometheus.Metrics.CreateCounter(
        "users_registered_total", "Total de usuários registrados");

    public static readonly Histogram RequestDuration = Prometheus.Metrics.CreateHistogram(
        "business_request_duration_seconds",
        "Duração das operações de negócio",
        new HistogramConfiguration
        {
            LabelNames = new[] { "operation" },
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 10)
        });
}
