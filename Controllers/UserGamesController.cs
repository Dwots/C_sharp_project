using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.DTOs;
using GameLibApi.Services.Interfaces;
using System.Security.Claims;

namespace GameLibApi.Controllers;

[ApiController]
[Route("api/users/{userId}/games")]
[Tags("User Library")]
public class UserGamesController : ControllerBase
{
    private readonly IUserGameService _service;
    private readonly ILogger<UserGamesController> _logger;

    public UserGamesController(IUserGameService service, ILogger<UserGamesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private (int? UserId, string? Role) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = int.TryParse(userIdStr, out var id) ? id : (int?)null;
        return (userId, role);
    }

    /// <summary>
    /// Получить библиотеку игр пользователя
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(IEnumerable<UserGameResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserGameResponseDto>>> GetUserLibrary(int userId)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        var library = await _service.GetUserLibraryAsync(userId, currentUserId, currentRole);
        return Ok(library);
    }

    /// <summary>
    /// Добавить игру в библиотеку
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(UserGameResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserGameResponseDto>> AddGame(int userId, [FromBody] AddGameToLibraryDto dto)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        var result = await _service.AddGameToLibraryAsync(userId, dto, currentUserId, currentRole);
        return Ok(result);
    }

    /// <summary>
    /// Обновить рейтинг игры
    /// </summary>
    [HttpPut("{gameId}")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateGame(int userId, int gameId, [FromBody] UpdateUserGameDto dto)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        await _service.UpdateUserGameAsync(userId, gameId, dto, currentUserId, currentRole);
        return NoContent();
    }

    /// <summary>
    /// Удалить игру из библиотеки
    /// </summary>
    [HttpDelete("{gameId}")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveGame(int userId, int gameId)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        await _service.RemoveGameFromLibraryAsync(userId, gameId, currentUserId, currentRole);
        return NoContent();
    }
}
