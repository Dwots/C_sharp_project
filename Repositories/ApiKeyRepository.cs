using GameLibApi.Data;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GameLibApi.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _context;

    public ApiKeyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByKeyAsync(string key)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == key && k.IsActive);
    }
}