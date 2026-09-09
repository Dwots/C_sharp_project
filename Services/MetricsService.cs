using Prometheus;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class MetricsService : IMetricsService
{
    private static readonly Counter GamesCreatedTotal = Metrics.CreateCounter(
        "gamelib_games_created_total",
        "Total number of games created");

    private static readonly Counter GamesDeletedTotal = Metrics.CreateCounter(
        "gamelib_games_deleted_total",
        "Total number of games deleted");

    private static readonly Counter UserRegistrationsTotal = Metrics.CreateCounter(
        "gamelib_user_registrations_total",
        "Total number of user registrations");

    private static readonly Counter LoginAttemptsTotal = Metrics.CreateCounter(
        "gamelib_login_attempts_total",
        "Total number of login attempts",
        new CounterConfiguration { LabelNames = new[] { "success" } });

    private static readonly Counter CacheHitsTotal = Metrics.CreateCounter(
        "gamelib_cache_hits_total",
        "Total number of cache hits",
        new CounterConfiguration { LabelNames = new[] { "cache_type" } });

    private static readonly Counter CacheMissesTotal = Metrics.CreateCounter(
        "gamelib_cache_misses_total",
        "Total number of cache misses",
        new CounterConfiguration { LabelNames = new[] { "cache_type" } });

    private static readonly Gauge ActiveUsersGauge = Metrics.CreateGauge(
        "gamelib_active_users",
        "Current number of active users");

    private static readonly Gauge TotalGamesGauge = Metrics.CreateGauge(
        "gamelib_total_games",
        "Total number of games in database");

    public void IncrementGamesCreated() => GamesCreatedTotal.Inc();
    public void IncrementGamesDeleted() => GamesDeletedTotal.Inc();
    public void IncrementUserRegistrations() => UserRegistrationsTotal.Inc();
    public void IncrementLoginAttempts(bool success) =>
        LoginAttemptsTotal.WithLabels(success.ToString().ToLower()).Inc();
    public void IncrementCacheHits(string cacheType) =>
        CacheHitsTotal.WithLabels(cacheType).Inc();
    public void IncrementCacheMisses(string cacheType) =>
        CacheMissesTotal.WithLabels(cacheType).Inc();
    public void SetActiveUsersCount(int count) => ActiveUsersGauge.Set(count);
    public void SetTotalGamesCount(int count) => TotalGamesGauge.Set(count);
}
