using Microsoft.EntityFrameworkCore;
using GameLibApi.Data;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;

namespace GameLibApi.Repositories;

public class GameRepository : IGameRepository
{
    private readonly AppDbContext _context;

    public GameRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Game>> GetAllAsync()
    {
        return await _context.Games
            .Include(g => g.GameCategories)
                .ThenInclude(gc => gc.Category)
            .ToListAsync();
    }

    public async Task<Game?> GetByIdAsync(int id)
    {
        return await _context.Games
            .Include(g => g.GameCategories)
                .ThenInclude(gc => gc.Category)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Game> CreateAsync(Game game)
    {
        _context.Games.Add(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task<Game?> UpdateAsync(Game game)
    {
        var existing = await _context.Games
            .Include(g => g.GameCategories)
            .FirstOrDefaultAsync(g => g.Id == game.Id);
            
        if (existing == null)
            return null;

        existing.Title = game.Title;
        existing.Description = game.Description;
        existing.Price = game.Price;
        existing.Developer = game.Developer;
        existing.ReleaseDate = game.ReleaseDate;
        
        // Обновляем категории
        existing.GameCategories.Clear();
        existing.GameCategories.AddRange(game.GameCategories);
        
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game == null)
            return false;

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _context.Games.AnyAsync(g => g.Id == id);
    }
    
    public async Task<(IEnumerable<Game> Items, int Total)> GetPagedAsync(GameFilterRequest request)
{
    var query = _context.Games
        .Include(g => g.GameCategories)
            .ThenInclude(gc => gc.Category)
        .AsQueryable();

    // Фильтрация по поиску (название или разработчик)
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
        var search = request.Search.ToLower();
        query = query.Where(g => 
            g.Title.ToLower().Contains(search) ||
            (g.Developer != null && g.Developer.ToLower().Contains(search)));
    }

    // Фильтр по цене
    if (request.MinPrice.HasValue)
    {
        query = query.Where(g => g.Price >= request.MinPrice.Value);
    }
    if (request.MaxPrice.HasValue)
    {
        query = query.Where(g => g.Price <= request.MaxPrice.Value);
    }

    // Фильтр по разработчику
    if (!string.IsNullOrWhiteSpace(request.Developer))
    {
        query = query.Where(g => g.Developer != null && 
            g.Developer.ToLower().Contains(request.Developer.ToLower()));
    }

    // Фильтр по категории
    if (request.CategoryId.HasValue)
    {
        query = query.Where(g => 
            g.GameCategories.Any(gc => gc.CategoryId == request.CategoryId.Value));
    }

    // Общее количество (до пагинации)
    var total = await query.CountAsync();

    // Сортировка
    query = request.SortBy?.ToLower() switch
    {
        "title" => request.SortDescending 
            ? query.OrderByDescending(g => g.Title) 
            : query.OrderBy(g => g.Title),
        "price" => request.SortDescending 
            ? query.OrderByDescending(g => g.Price) 
            : query.OrderBy(g => g.Price),
        "date" => request.SortDescending 
            ? query.OrderByDescending(g => g.ReleaseDate) 
            : query.OrderBy(g => g.ReleaseDate),
        _ => query.OrderBy(g => g.Id)
    };

    // Пагинация
    var items = await query
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToListAsync();

    return (items, total);
}
}