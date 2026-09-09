using System.Security.Claims;
using GameLibApi.Repositories.Interfaces;

namespace GameLibApi.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeader = "X-API-KEY";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValue))
        {
            var apiKey = apiKeyValue.ToString();

            using var scope = context.RequestServices.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

            var storedKey = await repository.GetByKeyAsync(apiKey);

            if (storedKey != null && storedKey.IsActive)
            {
                if (storedKey.ExpiresAt == null || storedKey.ExpiresAt > DateTime.UtcNow)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name, storedKey.Name),
                        new Claim(ClaimTypes.Role, storedKey.Role),
                        new Claim("AuthMethod", "ApiKey")
                    };

                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
                }
            }
        }

        await _next(context);
    }
}