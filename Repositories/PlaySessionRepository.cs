using Dapper;
using Npgsql;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;

namespace GameLibApi.Repositories;

/// <summary>
/// Репозиторий игровых сессий на Dapper.
///
/// Сознательно написан на явном SQL, а не на EF: play_sessions — основная
/// растущая таблица проекта, и все обращения к ней должны быть видимы
/// и пригодны для EXPLAIN ANALYZE. Запросы Query 1-3 и агрегаты собраны
/// здесь же, в константах, чтобы их можно было скопировать в psql как есть.
/// </summary>
public class PlaySessionRepository : IPlaySessionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PlaySessionRepository> _logger;

    public PlaySessionRepository(
        IConfiguration configuration,
        ILogger<PlaySessionRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    // ------------------------------------------------------------------
    // Query 1 / 2 / 3 — JOIN трёх таблиц + фильтры + сортировка + LIMIT.
    // Эндпоинт: GET /api/users/{userId}/sessions
    // ------------------------------------------------------------------
    public async Task<(IEnumerable<PlaySessionResponseDto> Items, int Total)> GetByUserAsync(
        int userId, PlaySessionFilterRequest filter)
    {
        // Условия собираются динамически: без фильтров это Query 1
        // (поиск по одному идентификатору), с фильтрами — Query 2.
        var conditions = new List<string> { "ps.user_id = @UserId" };

        if (!string.IsNullOrWhiteSpace(filter.Platform))
            conditions.Add("ps.platform = @Platform");
        if (filter.From.HasValue)
            conditions.Add("ps.created_at >= @From");
        if (filter.To.HasValue)
            conditions.Add("ps.created_at < @To");
        if (filter.MinDuration.HasValue)
            conditions.Add("ps.duration_minutes >= @MinDuration");

        var where = string.Join("\n              AND ", conditions);

        var itemsSql = $@"
            SELECT
                ps.id               AS Id,
                ps.user_id          AS UserId,
                u.username          AS Username,
                ps.game_id          AS GameId,
                g.title             AS GameTitle,
                ps.duration_minutes AS DurationMinutes,
                ps.platform         AS Platform,
                ps.created_at       AS CreatedAt
            FROM play_sessions ps
            JOIN users u ON u.id = ps.user_id
            JOIN games g ON g.id = ps.game_id
            WHERE {where}
            ORDER BY ps.created_at DESC
            LIMIT @Limit OFFSET @Offset";

        var countSql = $@"
            SELECT COUNT(*)
            FROM play_sessions ps
            WHERE {where}";

        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        parameters.Add("Platform", filter.Platform);
        parameters.Add("From", filter.From);
        parameters.Add("To", filter.To);
        parameters.Add("MinDuration", filter.MinDuration);
        parameters.Add("Limit", filter.PageSize);
        parameters.Add("Offset", (filter.Page - 1) * filter.PageSize);

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var items = await connection.QueryAsync<PlaySessionResponseDto>(itemsSql, parameters);
        var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        var result = items.ToList();
        _logger.LogInformation(
            "[Dapper] Sessions for user {UserId}: {Count} of {Total}", userId, result.Count, total);

        return (result, total);
    }

    public async Task<PlaySession> CreateAsync(PlaySession session)
    {
        const string sql = @"
            INSERT INTO play_sessions (user_id, game_id, duration_minutes, platform, created_at)
            VALUES (@UserId, @GameId, @DurationMinutes, @Platform, @CreatedAt)
            RETURNING id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            session.Id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                session.UserId,
                session.GameId,
                session.DurationMinutes,
                session.Platform,
                session.CreatedAt
            }, transaction: transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("[Dapper] Created play session {Id}", session.Id);

            return session;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error creating play session");
            throw;
        }
    }

    // ------------------------------------------------------------------
    // Агрегат №1 — JOIN четырёх таблиц + GROUP BY + COUNT/SUM/AVG.
    // Эндпоинт: GET /api/stats/top-games
    // ------------------------------------------------------------------
    public async Task<IEnumerable<GameStatsDto>> GetTopGamesAsync(int limit, DateTime? from)
    {
        // Сессии агрегируются в CTE ДО присоединения жанров. Если джойнить
        // game_categories сразу, каждая строка сессии размножится по числу
        // жанров игры и COUNT/SUM окажутся завышены в это же число раз.
        const string sql = @"
            WITH game_totals AS (
                SELECT
                    ps.game_id,
                    COUNT(*)                   AS sessions_count,
                    COUNT(DISTINCT ps.user_id) AS unique_players,
                    SUM(ps.duration_minutes)   AS total_minutes,
                    ROUND(AVG(ps.duration_minutes), 1)::float8 AS avg_minutes
                FROM play_sessions ps
                WHERE (@From::timestamp IS NULL OR ps.created_at >= @From::timestamp)
                GROUP BY ps.game_id
                ORDER BY total_minutes DESC
                LIMIT @Limit
            )
            SELECT
                g.id                              AS GameId,
                g.title                           AS GameTitle,
                string_agg(DISTINCT c.name, ', ') AS Categories,
                t.sessions_count                  AS SessionsCount,
                t.unique_players                  AS UniquePlayers,
                t.total_minutes                   AS TotalMinutes,
                t.avg_minutes                     AS AvgMinutes
            FROM game_totals t
            JOIN games g                 ON g.id = t.game_id
            LEFT JOIN game_categories gc ON gc.game_id = g.id
            LEFT JOIN categories c       ON c.id = gc.category_id
            GROUP BY g.id, g.title, t.sessions_count, t.unique_players,
                     t.total_minutes, t.avg_minutes
            ORDER BY t.total_minutes DESC";

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var result = (await connection.QueryAsync<GameStatsDto>(sql, new { From = from, Limit = limit })).ToList();
        _logger.LogInformation("[Dapper] Top games: {Count} rows", result.Count);

        return result;
    }

    // ------------------------------------------------------------------
    // Агрегат №2 — LEFT JOIN + GROUP BY по одному пользователю.
    // Эндпоинт: GET /api/users/{userId}/activity
    // ------------------------------------------------------------------
    public async Task<UserActivityDto?> GetUserActivityAsync(int userId)
    {
        const string sql = @"
            SELECT
                u.id                                   AS UserId,
                u.username                             AS Username,
                COUNT(ps.id)                           AS SessionsCount,
                COUNT(DISTINCT ps.game_id)             AS DistinctGames,
                COALESCE(SUM(ps.duration_minutes), 0)  AS TotalMinutes,
                MAX(ps.created_at)                     AS LastPlayedAt
            FROM users u
            LEFT JOIN play_sessions ps ON ps.user_id = u.id
            WHERE u.id = @UserId
            GROUP BY u.id, u.username";

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        return await connection.QueryFirstOrDefaultAsync<UserActivityDto>(sql, new { UserId = userId });
    }
}
