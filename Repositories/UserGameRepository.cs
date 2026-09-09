using GameLibApi.Data;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameLibApi.Repositories;

public class UserGameRepository : IUserGameRepository
{
    private readonly AppDbContext _context;

    public UserGameRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<UserGame>> GetUserGamesAsync(int userId)
    {
        return await _context.UserGames
            .Include(ug => ug.Game)
            .Include(ug => ug.User)
            .Where(ug => ug.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserGame?> GetUserGameAsync(int userId, int gameId)
    {
        return await _context.UserGames
            .Include(ug => ug.Game)
            .Include(ug => ug.User)
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GameId == gameId);
    }

    public async Task AddGameToLibraryAsync(UserGame userGame)
    {
        _context.UserGames.Add(userGame);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateUserGameAsync(UserGame userGame)
    {
        _context.UserGames.Update(userGame);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveGameFromLibraryAsync(int userId, int gameId)
    {
        var entity = await _context.UserGames
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GameId == gameId);
            
        if (entity != null)
        {
            _context.UserGames.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}