using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace GameLibApi.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private const string IdempotencyHeader = "Idempotency-Key";

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // Только для POST запросов
        if (context.Request.Method != HttpMethods.Post)
        {
            await _next(context);
            return;
        }

        // Проверяем наличие Idempotency-Key
        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        // Проверяем, был ли уже такой запрос
        var cachedResponse = await cache.GetStringAsync(cacheKey);
        if (cachedResponse != null)
        {
            _logger.LogInformation("Idempotency: Returning cached response for key {Key}", idempotencyKey);
            
            var cached = JsonSerializer.Deserialize<CachedResponse>(cachedResponse);
            context.Response.StatusCode = cached!.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        // Перехватываем ответ
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        // Сохраняем ответ в кэш
        memoryStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            var responseToCache = new CachedResponse
            {
                StatusCode = context.Response.StatusCode,
                Body = responseBody
            };

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(responseToCache),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                });

            _logger.LogInformation("Idempotency: Cached response for key {Key}", idempotencyKey);
        }

        // Копируем ответ обратно
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBodyStream);
        context.Response.Body = originalBodyStream;
    }

    private class CachedResponse
    {
        public int StatusCode { get; set; }
        public string Body { get; set; } = string.Empty;
    }
}