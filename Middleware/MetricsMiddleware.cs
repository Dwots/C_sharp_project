using System.Diagnostics;
using Prometheus;

namespace GameLibApi.Middleware;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    // Счётчик общего количества HTTP запросов
    private static readonly Counter HttpRequestsTotal = Metrics.CreateCounter(
        "gamelib_http_requests_total",
        "Total number of HTTP requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "endpoint", "status_code" }
        });

    // Гистограмма времени ответа
    private static readonly Histogram HttpRequestDuration = Metrics.CreateHistogram(
        "gamelib_http_request_duration_seconds",
        "HTTP request duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "endpoint" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 16) // от 1ms до ~65 секунд
        });

    // Gauge для активных запросов
    private static readonly Gauge HttpRequestsInProgress = Metrics.CreateGauge(
        "gamelib_http_requests_in_progress",
        "Number of HTTP requests currently being processed",
        new GaugeConfiguration
        {
            LabelNames = new[] { "method" }
        });

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Пропускаем сам эндпоинт /metrics
        if (context.Request.Path == "/metrics")
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var endpoint = GetNormalizedEndpoint(context);

        // Увеличиваем счётчик активных запросов
        HttpRequestsInProgress.WithLabels(method).Inc();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode.ToString();

            // Записываем метрики
            HttpRequestsTotal
                .WithLabels(method, endpoint, statusCode)
                .Inc();

            HttpRequestDuration
                .WithLabels(method, endpoint)
                .Observe(stopwatch.Elapsed.TotalSeconds);

            // Уменьшаем счётчик активных запросов
            HttpRequestsInProgress.WithLabels(method).Dec();
        }
    }

    /// <summary>
    /// Нормализует путь, заменяя ID на {id} для группировки метрик
    /// </summary>
    private static string GetNormalizedEndpoint(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Заменяем числовые ID на плейсхолдер {id}
        // /api/games/123 -> /api/games/{id}
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"/\d+",
            "/{id}");

        return normalized;
    }
}