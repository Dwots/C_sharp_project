using GameLibApi.Models;

namespace GameLibApi.Repositories.Interfaces;

public interface IUserGameRepository
{
    Task<IEnumerable<UserGame>> GetUserGamesAsync(int userId);
    Task<UserGame?> GetUserGameAsync(int userId, int gameId);
    Task AddGameToLibraryAsync(UserGame userGame);
    Task UpdateUserGameAsync(UserGame userGame);
    Task RemoveGameFromLibraryAsync(int userId, int gameId);
}