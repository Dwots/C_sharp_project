using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GameService> _logger;
    private readonly IMetricsService _metrics; // Добавили метрики

    private const string ALL_GAMES_CACHE_KEY = "games:all";
    private const string GAME_CACHE_KEY_PREFIX = "games:";

    public GameService(
        IGameRepository gameRepository,
        ICategoryRepository categoryRepository,
        IDistributedCache cache,
        ILogger<GameService> logger,
        IMetricsService metrics) // Инжектим
    {
        _gameRepository = gameRepository;
        _categoryRepository = categoryRepository;
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<IEnumerable<GameResponseDto>> GetAllAsync()
    {
        try
        {
            var cachedData = await _cache.GetStringAsync(ALL_GAMES_CACHE_KEY);
            if (cachedData != null)
            {
                _logger.LogInformation("Cache HIT for all games");
                _metrics.IncrementCacheHits("games_all"); // Метрика
                return JsonSerializer.Deserialize<List<GameResponseDto>>(cachedData)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error, loading from DB");
        }

        _logger.LogInformation("Cache MISS for all games, loading from DB");
        _metrics.IncrementCacheMisses("games_all"); // Метрика
        
        var games = await _gameRepository.GetAllAsync();
        var result = games.Select(MapToResponse).ToList();

        // Обновляем метрику общего количества игр
        _metrics.SetTotalGamesCount(result.Count);

        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
            await _cache.SetStringAsync(ALL_GAMES_CACHE_KEY, JsonSerializer.Serialize(result), options);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to cache data"); }

        return result;
    }

    public async Task<GameResponseDto?> GetByIdAsync(int id)
    {
        var cacheKey = $"{GAME_CACHE_KEY_PREFIX}{id}";

        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (cachedData != null)
            {
                _metrics.IncrementCacheHits("game_id");
                return JsonSerializer.Deserialize<GameResponseDto>(cachedData);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache error"); }

        _metrics.IncrementCacheMisses("game_id");
        var game = await _gameRepository.GetByIdAsync(id);
        if (game == null) return null;

        var result = MapToResponse(game);

        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), options);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to cache game"); }

        return result;
    }

    public async Task<GameResponseDto> CreateAsync(CreateGameDto dto)
    {
        var game = new Game
        {
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Developer = dto.Developer,
            ReleaseDate = dto.ReleaseDate,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.CategoryIds != null)
        {
            foreach (var categoryId in dto.CategoryIds)
            {
                if (await _categoryRepository.ExistsAsync(categoryId))
                {
                    game.GameCategories.Add(new GameCategory { CategoryId = categoryId });
                }
            }
        }

        var created = await _gameRepository.CreateAsync(game);
        _logger.LogInformation("Game created: {Title}", created.Title);
        
        // ВАЖНО: Увеличиваем счетчик метрик!
        _metrics.IncrementGamesCreated();

        await _cache.RemoveAsync(ALL_GAMES_CACHE_KEY);
        var result = await _gameRepository.GetByIdAsync(created.Id);
        return MapToResponse(result!);
    }

    public async Task<GameResponseDto?> UpdateAsync(int id, UpdateGameDto dto)
    {
        var existing = await _gameRepository.GetByIdAsync(id);
        if (existing == null) return null;

        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.Price = dto.Price;
        existing.Developer = dto.Developer;
        existing.ReleaseDate = dto.ReleaseDate;

        existing.GameCategories.Clear();
        if (dto.CategoryIds != null)
        {
            foreach (var categoryId in dto.CategoryIds)
            {
                if (await _categoryRepository.ExistsAsync(categoryId))
                {
                    existing.GameCategories.Add(new GameCategory { GameId = id, CategoryId = categoryId });
                }
            }
        }

        var updated = await _gameRepository.UpdateAsync(existing);
        
        await _cache.RemoveAsync($"{GAME_CACHE_KEY_PREFIX}{id}");
        await _cache.RemoveAsync(ALL_GAMES_CACHE_KEY);

        return MapToResponse(updated!);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _gameRepository.DeleteAsync(id);
        if (result)
        {
            // ВАЖНО: Увеличиваем счетчик удалений!
            _metrics.IncrementGamesDeleted();
            
            await _cache.RemoveAsync($"{GAME_CACHE_KEY_PREFIX}{id}");
            await _cache.RemoveAsync(ALL_GAMES_CACHE_KEY);
        }
        return result;
    }

    public async Task<PagedResponse<GameResponseDto>> GetPagedAsync(GameFilterRequest request)
    {
        var (items, total) = await _gameRepository.GetPagedAsync(request);
        return new PagedResponse<GameResponseDto>
        {
            Items = items.Select(MapToResponse).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static GameResponseDto MapToResponse(Game game)
    {
        return new GameResponseDto
        {
            Id = game.Id,
            Title = game.Title,
            Description = game.Description,
            Price = game.Price,
            Developer = game.Developer,
            ReleaseDate = game.ReleaseDate,
            CreatedAt = game.CreatedAt,
            Categories = game.GameCategories?.Select(gc => gc.Category?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>()
        };
    }
}
