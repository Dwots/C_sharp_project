namespace GameLibApi.Services.Sharding;

/// <summary>
/// Первая стратегия: shard = hash(shard_key) % N.
///
/// Самый простой способ распределить записи. Распределяет ровно, работает
/// мгновенно, ничего не хранит. Его слабое место проявляется только при
/// изменении N — см. часть 5 лабораторной.
/// </summary>
public class ModuloShardRouter : IShardRouter
{
    public string Strategy => $"hash(key) % {ShardCount}";
    public int ShardCount { get; }

    public ModuloShardRouter(int shardCount)
    {
        if (shardCount < 1)
            throw new ArgumentException("Шардов должно быть хотя бы один", nameof(shardCount));

        ShardCount = shardCount;
    }

    /// <summary>
    /// Считает хеш ключа и берёт остаток от деления на число шардов.
    ///
    /// Остаток от деления всегда попадает в диапазон 0..N-1 — ровно номера
    /// существующих шардов. Хеш беззнаковый, поэтому отрицательных значений
    /// не возникает и дополнительная проверка не нужна.
    /// </summary>
    public int GetShard(int shardKey) => (int)(ShardHash.Compute(shardKey) % (uint)ShardCount);
}
