using System.Diagnostics;

namespace GameLibApi.Middleware;

/// <summary>
/// Middleware для логирования всех входящих HTTP-запросов
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Пропускаем логирование для /metrics и /health (слишком частые)
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/metrics") || path.StartsWith("/health") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Собираем информацию о запросе
        var requestId = Guid.NewGuid().ToString("N")[..8]; // Короткий ID для трейсинга
        var method = context.Request.Method;
        var endpoint = context.Request.Path;
        var queryString = context.Request.QueryString;
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Получаем информацию о пользователе (если авторизован)
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var userName = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "anonymous";

        // Логируем начало запроса
        _logger.LogInformation(
            "[{RequestId}] ▶ {Method} {Endpoint}{QueryString} | User: {UserId} ({UserName}) | IP: {ClientIp}",
            requestId, method, endpoint, queryString, userId, userName, clientIp);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsed = stopwatch.ElapsedMilliseconds;

            // Выбираем уровень логирования в зависимости от статус кода
            if (statusCode >= 500)
            {
                _logger.LogError(
                    "[{RequestId}] ◀ {Method} {Endpoint} → {StatusCode} | {Elapsed}ms | ОШИБКА СЕРВЕРА",
                    requestId, method, endpoint, statusCode, elapsed);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "[{RequestId}] ◀ {Method} {Endpoint} → {StatusCode} | {Elapsed}ms | Ошибка клиента",
                    requestId, method, endpoint, statusCode, elapsed);
            }
            else
            {
                _logger.LogInformation(
                    "[{RequestId}] ◀ {Method} {Endpoint} → {StatusCode} | {Elapsed}ms",
                    requestId, method, endpoint, statusCode, elapsed);
            }
        }
    }
}