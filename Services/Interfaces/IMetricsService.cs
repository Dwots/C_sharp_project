namespace GameLibApi.Services.Interfaces;

/// <summary>
/// Сервис для бизнес-метрик приложения
/// </summary>
public interface IMetricsService
{
    void IncrementGamesCreated();
    void IncrementGamesDeleted();
    void IncrementUserRegistrations();
    void IncrementLoginAttempts(bool success);
    void IncrementCacheHits(string cacheType);
    void IncrementCacheMisses(string cacheType);
    void SetActiveUsersCount(int count);
    void SetTotalGamesCount(int count);
}
