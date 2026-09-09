using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameLibApi.DTOs;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Games")]
public class GameController : ControllerBase
{
    private readonly IGameService _service;
    private readonly ILogger<GameController> _logger;

    public GameController(IGameService service, ILogger<GameController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Получить все игры
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(IEnumerable<GameResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<GameResponseDto>>> GetAll()
    {
        var games = await _service.GetAllAsync();
        return Ok(games);
    }

    /// <summary>
    /// Получить игру по ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(GameResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GameResponseDto>> GetById(int id)
    {
        var game = await _service.GetByIdAsync(id);
        if (game == null) return NotFound();
        return Ok(game);
    }

    /// <summary>
    /// Создать игру
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(GameResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GameResponseDto>> Create([FromBody] CreateGameDto dto)
    {
        var game = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, game);
    }

    /// <summary>
    /// Обновить игру
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGameDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result == null) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Удалить игру
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

    /// <summary>
    /// Получить игры с пагинацией и фильтрацией
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Roles = "Admin,Manager,User")]
    [ProducesResponseType(typeof(PagedResponse<GameResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<GameResponseDto>>> GetPaged([FromQuery] GameFilterRequest request)
    {
        var result = await _service.GetPagedAsync(request);
        return Ok(result);
    }
}