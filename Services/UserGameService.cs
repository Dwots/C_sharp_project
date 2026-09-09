using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class UserGameService : IUserGameService
{
    private readonly IUserGameRepository _userGameRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<UserGameService> _logger;

    private const string USER_LIBRARY_CACHE_KEY_PREFIX = "user:library:";

    public UserGameService(
        IUserGameRepository userGameRepository,
        IGameRepository gameRepository,
        IUserRepository userRepository,
        IDistributedCache cache,
        ILogger<UserGameService> logger)
    {
        _userGameRepository = userGameRepository;
        _gameRepository = gameRepository;
        _userRepository = userRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<UserGameResponseDto>> GetUserLibraryAsync(int userId, int? currentUserId, string? currentRole)
    {
        if (currentRole == "User" && currentUserId != userId)
        {
            throw new UnauthorizedAccessException("Нет доступа к библиотеке другого пользователя");
        }

        var cacheKey = $"{USER_LIBRARY_CACHE_KEY_PREFIX}{userId}";

        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("Cache HIT for user {UserId} library", userId);
                return JsonSerializer.Deserialize<List<UserGameResponseDto>>(cachedData)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for user {UserId} library", userId);
        }

        _logger.LogInformation("Cache MISS for user {UserId} library", userId);
        var userGames = await _userGameRepository.GetUserGamesAsync(userId);

        var result = userGames.Select(ug => new UserGameResponseDto
        {
            UserId = ug.UserId,
            Username = ug.User?.Username ?? "Unknown",
            GameId = ug.GameId,
            GameTitle = ug.Game?.Title ?? "Unknown",
            Rating = ug.Rating,
            AddedAt = ug.AddedAt
        }).ToList();

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache user {UserId} library", userId);
        }

        return result;
    }

    public async Task<UserGameResponseDto> AddGameToLibraryAsync(int userId, AddGameToLibraryDto dto, int? currentUserId, string? currentRole)
    {
        // Проверка доступа
        if (currentRole == "User" && currentUserId != userId)
        {
            throw new UnauthorizedAccessException("Нельзя добавить игру в библиотеку другого пользователя");
        }

        if (!await _userRepository.ExistsAsync(userId))
            throw new KeyNotFoundException("Пользователь не найден");

        if (!await _gameRepository.ExistsAsync(dto.GameId))
            throw new KeyNotFoundException("Игра не найдена");

        var existing = await _userGameRepository.GetUserGameAsync(userId, dto.GameId);
        if (existing != null)
            throw new InvalidOperationException("Игра уже в библиотеке");

        var userGame = new UserGame
        {
            UserId = userId,
            GameId = dto.GameId,
            Rating = dto.Rating,
            AddedAt = DateTime.UtcNow
        };

        await _userGameRepository.AddGameToLibraryAsync(userGame);
        _logger.LogInformation("User {UserId} added game {GameId} to library", userId, dto.GameId);

        await _cache.RemoveAsync($"{USER_LIBRARY_CACHE_KEY_PREFIX}{userId}");

        var created = await _userGameRepository.GetUserGameAsync(userId, dto.GameId);

        return new UserGameResponseDto
        {
            UserId = created!.UserId,
            Username = created.User.Username,
            GameId = created.GameId,
            GameTitle = created.Game.Title,
            Rating = created.Rating,
            AddedAt = created.AddedAt
        };
    }

    public async Task UpdateUserGameAsync(int userId, int gameId, UpdateUserGameDto dto, int? currentUserId, string? currentRole)
    {
        if (currentRole == "User" && currentUserId != userId)
        {
            throw new UnauthorizedAccessException("Нельзя изменить рейтинг в библиотеке другого пользователя");
        }

        var userGame = await _userGameRepository.GetUserGameAsync(userId, gameId);
        if (userGame == null)
            throw new KeyNotFoundException("Игра не найдена в библиотеке");

        userGame.Rating = dto.Rating;
        await _userGameRepository.UpdateUserGameAsync(userGame);
        _logger.LogInformation("User {UserId} updated rating for game {GameId}", userId, gameId);

        await _cache.RemoveAsync($"{USER_LIBRARY_CACHE_KEY_PREFIX}{userId}");
    }

    public async Task RemoveGameFromLibraryAsync(int userId, int gameId, int? currentUserId, string? currentRole)
    {
        if (currentRole == "User" && currentUserId != userId)
        {
            throw new UnauthorizedAccessException("Нельзя удалить игру из библиотеки другого пользователя");
        }

        await _userGameRepository.RemoveGameFromLibraryAsync(userId, gameId);
        _logger.LogInformation("User {UserId} removed game {GameId} from library", userId, gameId);

        await _cache.RemoveAsync($"{USER_LIBRARY_CACHE_KEY_PREFIX}{userId}");
    }
}
