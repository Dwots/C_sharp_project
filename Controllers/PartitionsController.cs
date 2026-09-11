using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.Models;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Controllers;

/// <summary>
/// Ручное управление партициями. Нужно, чтобы не ждать ночного запуска job
/// и показать полный сценарий на защите: создание → проверка → сбой → alert →
/// восстановление → OK.
/// </summary>
[ApiController]
[Route("api/partitions")]
[Tags("Partitions")]
public class PartitionsController : ControllerBase
{
    private readonly IPartitionService _partitions;
    private readonly IAlertService _alerts;

    public PartitionsController(IPartitionService partitions, IAlertService alerts)
    {
        _partitions = partitions;
        _alerts = alerts;
    }

    /// <summary>
    /// Какие партиции ожидаются и какие из них существуют, по всем настроенным таблицам.
    /// </summary>
    [HttpGet("status")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(IEnumerable<PartitionHealthResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PartitionHealthResult>>> GetStatus()
    {
        return Ok(await _partitions.CheckAllAsync(DateTime.UtcNow));
    }

    /// <summary>
    /// Запускает создание недостающих партиций (часть 10).
    /// Повторный вызов безопасен: уже существующие партиции не трогаются.
    /// </summary>
    [HttpPost("create-missing")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<PartitionJobResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PartitionJobResult>>> CreateMissing()
    {
        var now = DateTime.UtcNow;
        var results = new List<PartitionJobResult>();

        foreach (var target in _partitions.Targets)
            results.Add(await _partitions.CreateMissingPartitionsAsync(target, now));

        return Ok(results);
    }

    /// <summary>
    /// Проверяет состояние партиций и при смене состояния отправляет уведомление
    /// (часть 11). Повторные проверки в том же состоянии уведомление не шлют.
    /// </summary>
    [HttpPost("check")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Check()
    {
        var now = DateTime.UtcNow;
        var results = await _partitions.CheckAllAsync(now);

        var report = new List<object>();
        foreach (var health in results)
        {
            var sent = await _alerts.ProcessHealthResultAsync(health);
            report.Add(new
            {
                table = health.Table,
                status = health.Status,
                missing = health.Missing,
                alertSent = sent,
                alertChannelConfigured = _alerts.IsConfigured
            });
        }

        return Ok(report);
    }

    /// <summary>
    /// Удаляет партицию, чтобы искусственно создать сбой и проверить алертинг
    /// (пункт 11.2 задания). Операция разрушительная и доступна только Admin.
    /// </summary>
    /// <param name="schema">Схема, например public.</param>
    /// <param name="name">Имя партиции, например play_sessions_2026_12.</param>
    [HttpDelete("{schema}/{name}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DropPartition(string schema, string name)
    {
        await _partitions.DropPartitionAsync(schema, name);
        return NoContent();
    }

    /// <summary>
    /// Сбрасывает запомненное состояние алертов, чтобы прогнать сценарий заново.
    /// </summary>
    [HttpPost("reset-alert-state")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ResetAlertState()
    {
        _alerts.ResetState();
        return NoContent();
    }
}
