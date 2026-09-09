using GameLibApi.DTOs;
using GameLibApi.Models;

namespace GameLibApi.Repositories.Interfaces;

public interface IPlaySessionRepository
{
    /// <summary>Query 1 / 2 / 3: история сессий пользователя с фильтрами и пагинацией.</summary>
    Task<(IEnumerable<PlaySessionResponseDto> Items, int Total)> GetByUserAsync(
        int userId, PlaySessionFilterRequest filter);

    Task<PlaySession> CreateAsync(PlaySession session);

    /// <summary>Агрегат: топ игр по суммарному времени за период.</summary>
    Task<IEnumerable<GameStatsDto>> GetTopGamesAsync(int limit, DateTime? from);

    /// <summary>Агрегат: сводная активность одного пользователя.</summary>
    Task<UserActivityDto?> GetUserActivityAsync(int userId);
}
