using System.Net;
using System.Text.Json;

namespace GameLibApi.Middleware;

/// <summary>
/// Глобальный обработчик исключений
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorType, message) = exception switch
        {
            // Бизнес-ошибки
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, "Conflict", exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "BadRequest", exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NotFound", exception.Message),
            
            // Всё остальное — ошибка сервера
            _ => (HttpStatusCode.InternalServerError, "InternalServerError", "Произошла внутренняя ошибка сервера")
        };

        // Логируем ошибку
        _logger.LogError(
            exception,
            "❌ EXCEPTION | Type: {ExceptionType} | Message: {Message} | Path: {Path} | Method: {Method}",
            exception.GetType().Name,
            exception.Message,
            context.Request.Path,
            context.Request.Method);

        // Формируем ответ
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = errorType,
            message = message,
            // Stack trace только в Development
            details = _env.IsDevelopment() ? exception.StackTrace : null,
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.Value
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}