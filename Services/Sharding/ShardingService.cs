using Dapper;
using Npgsql;

namespace GameLibApi.Services.Sharding;

public record ShardCount(int Shard, string Host, long Rows);

public record DistributionResult(
    string Strategy,
    int TotalRead,
    int TotalWritten,
    List<ShardCount> PerShard,
    double SkewPercent,
    long ElapsedMs);

public record StrategyComparison(
    string Strategy,
    int FromShards,
    int ToShards,
    int TotalKeys,
    int Moved,
    double MovedPercent);

/// <summary>
/// Распределение данных по шардам и сравнение стратегий (лабораторная №5).
///
/// Источник данных — основная база проекта: методичка требует работать
/// со своей сущностью, а не с искусственно сгенерированной.
/// </summary>
public class ShardingService
{
    private readonly string _sourceConnectionString;
    private readonly List<string> _shardConnectionStrings;
    private readonly ILogger<ShardingService> _logger;

    public ShardingService(IConfiguration configuration, ILogger<ShardingService> logger)
    {
        _sourceConnectionString = configuration.GetConnectionString("DefaultConnection")!;
        _shardConnectionStrings = configuration.GetSection("Sharding:Shards").Get<List<string>>() ?? new();
        _logger = logger;
    }

    public int ShardCount => _shardConnectionStrings.Count;

    /// <summary>Router по имени стратегии, для заданного числа шардов.</summary>
    public IShardRouter CreateRouter(string strategy, int shardCount) =>
        strategy.Equals("consistent", StringComparison.OrdinalIgnoreCase)
            ? new ConsistentHashRouter(shardCount)
            : new ModuloShardRouter(shardCount);

    // ------------------------------------------------------------------
    // Часть 4: разложить данные по шардам
    // ------------------------------------------------------------------
    public async Task<DistributionResult> DistributeAsync(string strategy, int limit)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var router = CreateRouter(strategy, ShardCount);

        // Читаем реальные сессии из основной базы.
        await using var source = new NpgsqlConnection(_sourceConnectionString);
        var rows = (await source.QueryAsync<(long Id, int UserId, int GameId, int DurationMinutes, string Platform, DateTime CreatedAt)>(
            @"SELECT id, user_id, game_id, duration_minutes, platform, created_at
              FROM play_sessions
              ORDER BY id
              LIMIT @Limit", new { Limit = limit })).ToList();

        // Чистим шарды, чтобы повторный запуск давал тот же результат,
        // а не накапливал дубли.
        foreach (var cs in _shardConnectionStrings)
        {
            await using var c = new NpgsqlConnection(cs);
            await c.ExecuteAsync("TRUNCATE play_sessions");
        }

        // Раскладываем по корзинам: router решает, куда пойдёт каждая запись.
        var buckets = Enumerable.Range(0, ShardCount)
            .ToDictionary(i => i, _ => new List<(long Id, int UserId, int GameId, int DurationMinutes, string Platform, DateTime CreatedAt)>());

        foreach (var r in rows)
            buckets[router.GetShard(r.UserId)].Add(r);

        // Пишем каждую корзину в свой шард потоковой загрузкой COPY.
        // Построчный INSERT здесь неприменим: 100 000 записей означали бы
        // столько же отдельных обращений к базе (проверено — 137 секунд).
        // COPY передаёт весь поток по одному соединению.
        //
        // Внимание: это НЕ одна транзакция. Серверы независимы, общего
        // координатора нет, и если запись во второй шард упадёт после
        // успешной записи в первый, откатить первую уже нельзя. Именно
        // такие распределённые транзакции и являются главной сложностью
        // шардирования.
        var written = 0;
        for (var shard = 0; shard < ShardCount; shard++)
        {
            if (buckets[shard].Count == 0) continue;

            await using var c = new NpgsqlConnection(_shardConnectionStrings[shard]);
            await c.OpenAsync();

            await using (var writer = await c.BeginBinaryImportAsync(
                @"COPY play_sessions (id, user_id, game_id, duration_minutes, platform, created_at)
                  FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var r in buckets[shard])
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(r.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
                    await writer.WriteAsync(r.UserId, NpgsqlTypes.NpgsqlDbType.Integer);
                    await writer.WriteAsync(r.GameId, NpgsqlTypes.NpgsqlDbType.Integer);
                    await writer.WriteAsync(r.DurationMinutes, NpgsqlTypes.NpgsqlDbType.Integer);
                    await writer.WriteAsync(r.Platform, NpgsqlTypes.NpgsqlDbType.Varchar);
                    await writer.WriteAsync(r.CreatedAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
                }

                await writer.CompleteAsync();
            }

            written += buckets[shard].Count;
        }

        var perShard = await GetDistributionAsync();
        started.Stop();

        _logger.LogInformation(
            "Шардирование: стратегия={Strategy}, прочитано={Read}, записано={Written}",
            router.Strategy, rows.Count, written);

        return new DistributionResult(
            router.Strategy, rows.Count, written, perShard,
            CalculateSkew(perShard), started.ElapsedMilliseconds);
    }

    /// <summary>Сколько записей реально лежит на каждом шарде.</summary>
    public async Task<List<ShardCount>> GetDistributionAsync()
    {
        var result = new List<ShardCount>();

        for (var i = 0; i < ShardCount; i++)
        {
            await using var c = new NpgsqlConnection(_shardConnectionStrings[i]);
            var rows = await c.ExecuteScalarAsync<long>("SELECT count(*) FROM play_sessions");
            var host = new NpgsqlConnectionStringBuilder(_shardConnectionStrings[i]).Host ?? "?";
            result.Add(new ShardCount(i, host!, rows));
        }

        return result;
    }

    /// <summary>
    /// Перекос распределения: на сколько процентов самый нагруженный шард
    /// отличается от идеально равной доли.
    /// </summary>
    private static double CalculateSkew(List<ShardCount> shards)
    {
        if (shards.Count == 0) return 0;

        var total = shards.Sum(s => s.Rows);
        if (total == 0) return 0;

        var ideal = (double)total / shards.Count;
        var maxDeviation = shards.Max(s => Math.Abs(s.Rows - ideal));

        return Math.Round(maxDeviation / ideal * 100, 2);
    }

    // ------------------------------------------------------------------
    // Части 5 и 7: сколько записей сменит шард при изменении их числа
    // ------------------------------------------------------------------
    public async Task<StrategyComparison> CompareAsync(string strategy, int fromShards, int toShards, int limit)
    {
        var before = CreateRouter(strategy, fromShards);
        var after = CreateRouter(strategy, toShards);

        // Берём те же реальные записи, что распределяли в части 4.
        await using var source = new NpgsqlConnection(_sourceConnectionString);
        var userIds = (await source.QueryAsync<int>(
            "SELECT user_id FROM play_sessions ORDER BY id LIMIT @Limit", new { Limit = limit })).ToList();

        // Ключ «переехал», если старый и новый router дали разные номера шардов.
        var moved = userIds.Count(uid => before.GetShard(uid) != after.GetShard(uid));

        return new StrategyComparison(
            before.Strategy, fromShards, toShards, userIds.Count,
            moved, Math.Round(100.0 * moved / Math.Max(userIds.Count, 1), 2));
    }
}
