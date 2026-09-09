using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class PlaySessionService : IPlaySessionService
{
    private readonly IPlaySessionRepository _repository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PlaySessionService> _logger;

    public PlaySessionService(
        IPlaySessionRepository repository,
        IGameRepository gameRepository,
        IUserRepository userRepository,
        ILogger<PlaySessionService> logger)
    {
        _repository = repository;
        _gameRepository = gameRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Обычный пользователь видит только свои сессии, Admin и Manager — любые.
    /// </summary>
    private static void EnsureCanAccess(int userId, int? currentUserId, string? currentRole)
    {
        if (currentRole == "User" && currentUserId != userId)
        {
            throw new UnauthorizedAccessException("Нет доступа к сессиям другого пользователя");
        }
    }

    public async Task<PagedResponse<PlaySessionResponseDto>> GetUserSessionsAsync(
        int userId, PlaySessionFilterRequest filter, int? currentUserId, string? currentRole)
    {
        EnsureCanAccess(userId, currentUserId, currentRole);

        // Кэш здесь намеренно не используется: это основной запрос, на котором
        // измеряется производительность БД в лабораторных работах.
        var (items, total) = await _repository.GetByUserAsync(userId, filter);

        return new PagedResponse<PlaySessionResponseDto>
        {
            Items = items.ToList(),
            Total = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PlaySessionResponseDto> CreateAsync(
        int userId, CreatePlaySessionDto dto, int? currentUserId, string? currentRole)
    {
        EnsureCanAccess(userId, currentUserId, currentRole);

        var user = await _userRepository.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"Пользователь {userId} не найден");

        var game = await _gameRepository.GetByIdAsync(dto.GameId)
                   ?? throw new KeyNotFoundException($"Игра {dto.GameId} не найдена");

        var session = await _repository.CreateAsync(new PlaySession
        {
            UserId = userId,
            GameId = dto.GameId,
            DurationMinutes = dto.DurationMinutes,
            Platform = dto.Platform,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Play session {Id} created: user {UserId}, game {GameId}", session.Id, userId, dto.GameId);

        return new PlaySessionResponseDto
        {
            Id = session.Id,
            UserId = session.UserId,
            Username = user.Username,
            GameId = session.GameId,
            GameTitle = game.Title,
            DurationMinutes = session.DurationMinutes,
            Platform = session.Platform,
            CreatedAt = session.CreatedAt
        };
    }

    public async Task<IEnumerable<GameStatsDto>> GetTopGamesAsync(int limit, int? days)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentException("limit должен быть от 1 до 100");

        var from = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : (DateTime?)null;
        return await _repository.GetTopGamesAsync(limit, from);
    }

    public async Task<UserActivityDto> GetUserActivityAsync(int userId, int? currentUserId, string? currentRole)
    {
        EnsureCanAccess(userId, currentUserId, currentRole);

        return await _repository.GetUserActivityAsync(userId)
               ?? throw new KeyNotFoundException($"Пользователь {userId} не найден");
    }
}
