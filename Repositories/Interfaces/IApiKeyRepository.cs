using GameLibApi.Models;

namespace GameLibApi.Repositories.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByKeyAsync(string key);
}