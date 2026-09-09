using GameLibApi.DTOs;

namespace GameLibApi.Services.Interfaces;

public interface IPlaySessionService
{
    Task<PagedResponse<PlaySessionResponseDto>> GetUserSessionsAsync(
        int userId, PlaySessionFilterRequest filter, int? currentUserId, string? currentRole);

    Task<PlaySessionResponseDto> CreateAsync(
        int userId, CreatePlaySessionDto dto, int? currentUserId, string? currentRole);

    Task<IEnumerable<GameStatsDto>> GetTopGamesAsync(int limit, int? days);

    Task<UserActivityDto> GetUserActivityAsync(int userId, int? currentUserId, string? currentRole);
}
