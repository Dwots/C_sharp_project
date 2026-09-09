using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CategoryService> _logger;

    private const string ALL_CATEGORIES_CACHE_KEY = "categories:all";
    private const string CATEGORY_CACHE_KEY_PREFIX = "categories:";

    public CategoryService(
        ICategoryRepository repository,
        IDistributedCache cache,
        ILogger<CategoryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<CategoryResponseDto>> GetAllAsync()
    {
        try
        {
            // Попытка получить из кэша
            var cachedData = await _cache.GetStringAsync(ALL_CATEGORIES_CACHE_KEY);
            if (cachedData != null)
            {
                _logger.LogInformation("Cache HIT for all categories");
                return JsonSerializer.Deserialize<List<CategoryResponseDto>>(cachedData)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error, loading from DB");
        }

        // Запрос в БД
        _logger.LogInformation("Cache MISS for all categories, loading from DB");
        var categories = await _repository.GetAllAsync();
        var result = categories.Select(MapToResponse).ToList();

        try
        {
            // Сохранить в кэш на 30 минут (категории меняются редко)
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(
                ALL_CATEGORIES_CACHE_KEY,
                JsonSerializer.Serialize(result),
                options);
            _logger.LogInformation("Cached {Count} categories", result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache categories");
        }

        return result;
    }

    public async Task<CategoryResponseDto?> GetByIdAsync(int id)
    {
        var cacheKey = $"{CATEGORY_CACHE_KEY_PREFIX}{id}";

        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey);
            if (cachedData != null)
            {
                _logger.LogInformation("Cache HIT for category {Id}", id);
                return JsonSerializer.Deserialize<CategoryResponseDto>(cachedData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for category {Id}", id);
        }

        _logger.LogInformation("Cache MISS for category {Id}", id);
        var category = await _repository.GetByIdAsync(id);
        if (category == null)
        {
            _logger.LogWarning("Category with ID {Id} not found", id);
            return null;
        }

        var result = MapToResponse(category);

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache category {Id}", id);
        }

        return result;
    }

    public async Task<CategoryResponseDto> CreateAsync(CreateCategoryDto dto)
    {
        var category = new Category
        {
            Name = dto.Name,
            Description = dto.Description
        };

        var created = await _repository.CreateAsync(category);
        _logger.LogInformation("Category created with ID {Id}", created.Id);

        // Инвалидировать кэш
        await _cache.RemoveAsync(ALL_CATEGORIES_CACHE_KEY);

        return MapToResponse(created);
    }

    public async Task<CategoryResponseDto?> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        var category = new Category
        {
            Id = id,
            Name = dto.Name,
            Description = dto.Description
        };

        var updated = await _repository.UpdateAsync(category);
        if (updated == null)
        {
            _logger.LogWarning("Category with ID {Id} not found for update", id);
            return null;
        }

        _logger.LogInformation("Category with ID {Id} updated", id);

        // Инвалидировать кэш
        await _cache.RemoveAsync($"{CATEGORY_CACHE_KEY_PREFIX}{id}");
        await _cache.RemoveAsync(ALL_CATEGORIES_CACHE_KEY);

        return MapToResponse(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _repository.DeleteAsync(id);
        if (!result)
        {
            _logger.LogWarning("Category with ID {Id} not found for deletion", id);
        }
        else
        {
            _logger.LogInformation("Category with ID {Id} deleted", id);

            // Инвалидировать кэш
            await _cache.RemoveAsync($"{CATEGORY_CACHE_KEY_PREFIX}{id}");
            await _cache.RemoveAsync(ALL_CATEGORIES_CACHE_KEY);
        }
        return result;
    }

    private static CategoryResponseDto MapToResponse(Category category)
    {
        return new CategoryResponseDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            GamesCount = category.GameCategories?.Count ?? 0
        };
    }
}