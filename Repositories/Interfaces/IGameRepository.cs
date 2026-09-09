using GameLibApi.DTOs;
using GameLibApi.Models;

namespace GameLibApi.Repositories.Interfaces;

public interface IGameRepository
{
    Task<IEnumerable<Game>> GetAllAsync();
    Task<Game?> GetByIdAsync(int id);
    Task<Game> CreateAsync(Game game);
    Task<Game?> UpdateAsync(Game game);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<(IEnumerable<Game> Items, int Total)> GetPagedAsync(GameFilterRequest request);
}