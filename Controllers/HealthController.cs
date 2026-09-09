using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameLibApi.Controllers;

/// <summary>
/// Health Check эндпоинт
/// </summary>
[ApiController]
[Route("[controller]")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Проверка состояния API, PostgreSQL и Redis
    /// </summary>
    /// <returns>Статус всех компонентов</returns>
    [HttpGet("/health")]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    [ProducesResponseType(typeof(HealthResponse), 503)]
    public async Task<IActionResult> GetHealth()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        var response = new HealthResponse
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Duration = $"{report.TotalDuration.TotalMilliseconds:F2}ms",
            Checks = report.Entries.Select(e => new HealthCheckItem
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = $"{e.Value.Duration.TotalMilliseconds:F2}ms",
                Error = e.Value.Exception?.Message
            }).ToList()
        };

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Duration { get; set; } = string.Empty;
    public List<HealthCheckItem> Checks { get; set; } = new();
}

public class HealthCheckItem
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string? Error { get; set; }
}