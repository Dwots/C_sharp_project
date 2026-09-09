using GameLibApi.DTOs;

namespace GameLibApi.Services.Interfaces;

public interface IUserGameService
{
    Task<IEnumerable<UserGameResponseDto>> GetUserLibraryAsync(int userId, int? currentUserId, string? currentRole);
    Task<UserGameResponseDto> AddGameToLibraryAsync(int userId, AddGameToLibraryDto dto, int? currentUserId, string? currentRole);
    Task UpdateUserGameAsync(int userId, int gameId, UpdateUserGameDto dto, int? currentUserId, string? currentRole);
    Task RemoveGameFromLibraryAsync(int userId, int gameId, int? currentUserId, string? currentRole);
}
