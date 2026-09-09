using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.DTOs;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Users")]
public class UserController : ControllerBase
{
    private readonly IUserService _service;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService service, ILogger<UserController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Получить данные текущего пользователя из JWT
    /// </summary>
    private (int? UserId, string? Role) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var userId = int.TryParse(userIdStr, out var id) ? id : (int?)null;
        return (userId, role);
    }

    /// <summary>
    /// Получить всех пользователей (только Admin и Manager)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAll()
    {
        var users = await _service.GetAllAsync();
        return Ok(users);
    }

    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    /// <remarks>
    /// - Admin и Manager могут просматривать любого пользователя
    /// - User может просматривать только свой профиль
    /// </remarks>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponseDto>> GetById(int id)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        var user = await _service.GetByIdAsync(id, currentUserId, currentRole);
        if (user == null) return NotFound();
        return Ok(user);
    }

    /// <summary>
    /// Создать пользователя (только Admin)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponseDto>> Create([FromBody] CreateUserDto dto)
    {
        var user = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>
    /// Обновить пользователя
    /// </summary>
    /// <remarks>
    /// - Admin и Manager могут редактировать любого пользователя
    /// - User может редактировать только свой профиль
    /// </remarks>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
    {
        var (currentUserId, currentRole) = GetCurrentUser();
        var result = await _service.UpdateAsync(id, dto, currentUserId, currentRole);
        if (result == null) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Удалить пользователя (только Admin)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }
}
