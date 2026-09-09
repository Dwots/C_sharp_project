using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.DTOs;
using GameLibApi.Services.Interfaces;
using System.Security.Claims;

namespace GameLibApi.Controllers;

[ApiController]
[Route("api/users/{userId}/sessions")]
[Tags("Play Sessions")]
public class PlaySessionsController : ControllerBase
{
    private readonly IPlaySessionService _service;

    public PlaySessionsController(IPlaySessionService service)
    {
        _service = service;
    }

    private (int? UserId, string? Role) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = int.TryParse(userIdStr, out var id) ? id : (int?)null;
        return (userId, role);
    }

    /// <summary>
    /// История игровых сессий пользователя: пагинация, фильтры, сортировка по дате.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(PagedResponse<PlaySessionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PlaySessionResponseDto>>> GetSessions(
        int userId, [FromQuery] PlaySessionFilterRequest filter)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        return Ok(await _service.GetUserSessionsAsync(userId, filter, currentUserId, currentRole));
    }

    /// <summary>
    /// Записать новую игровую сессию.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(PlaySessionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaySessionResponseDto>> Create(
        int userId, [FromBody] CreatePlaySessionDto dto)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        var created = await _service.CreateAsync(userId, dto, currentUserId, currentRole);
        return CreatedAtAction(nameof(GetSessions), new { userId }, created);
    }

    /// <summary>
    /// Сводная активность пользователя (агрегат).
    /// </summary>
    [HttpGet("/api/users/{userId}/activity")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(UserActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserActivityDto>> GetActivity(int userId)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        return Ok(await _service.GetUserActivityAsync(userId, currentUserId, currentRole));
    }
}

[ApiController]
[Route("api/stats")]
[Tags("Statistics")]
public class StatsController : ControllerBase
{
    private readonly IPlaySessionService _service;

    public StatsController(IPlaySessionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Топ игр по суммарному наигранному времени (агрегат по четырём таблицам).
    /// </summary>
    /// <param name="limit">Сколько игр вернуть, 1-100.</param>
    /// <param name="days">Учитывать сессии за последние N дней. Без параметра — за всё время.</param>
    [HttpGet("top-games")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(IEnumerable<GameStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<GameStatsDto>>> GetTopGames(
        [FromQuery] int limit = 10, [FromQuery] int? days = null)
    {
        return Ok(await _service.GetTopGamesAsync(limit, days));
    }
}
