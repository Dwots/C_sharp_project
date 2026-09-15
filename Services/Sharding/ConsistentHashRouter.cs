namespace GameLibApi.Services.Sharding;

/// <summary>
/// Вторая стратегия: Consistent Hashing.
///
/// Идея: хеши образуют кольцо значений 0 .. 2^32-1. На это кольцо ставятся
/// точки шардов, ключ тоже превращается в точку, и запись уходит на первый
/// шард по часовой стрелке.
///
/// Отличие от hash(key) % N в том, что позиции шардов на кольце не зависят
/// от их количества. Добавление шарда вставляет на кольцо новые точки и
/// «отрезает» им часть чужих секторов — переезжают только те ключи, что попали
/// в эти куски. Остальные остаются на месте, потому что их ближайший шард
/// по кольцу не изменился.
///
/// Virtual nodes: каждый шард представлен не одной точкой, а множеством.
/// С одной точкой на шард распределение получилось бы рваным — сектора между
/// случайными точками сильно различаются по длине. Сотня точек на шард
/// усредняет длины и выравнивает доли.
/// </summary>
public class ConsistentHashRouter : IShardRouter
{
    private const int VirtualNodesPerShard = 100;

    /// <summary>Точки кольца, отсортированные по возрастанию хеша.</summary>
    private readonly uint[] _ringPositions;

    /// <summary>Какому шарду принадлежит точка с тем же индексом.</summary>
    private readonly int[] _ringShards;

    public string Strategy => $"Consistent Hashing ({ShardCount} шардов × {VirtualNodesPerShard} точек)";
    public int ShardCount { get; }

    public ConsistentHashRouter(int shardCount)
    {
        if (shardCount < 1)
            throw new ArgumentException("Шардов должно быть хотя бы один", nameof(shardCount));

        ShardCount = shardCount;

        // Расставляем точки: для каждого шарда считаем хеши "shard-0#0",
        // "shard-0#1" и так далее. Позиция зависит только от имени шарда
        // и номера точки — но не от общего количества шардов. Именно поэтому
        // добавление нового шарда не сдвигает существующие точки.
        var points = new List<(uint Position, int Shard)>(shardCount * VirtualNodesPerShard);

        for (var shard = 0; shard < shardCount; shard++)
        for (var vnode = 0; vnode < VirtualNodesPerShard; vnode++)
            points.Add((ShardHash.Compute($"shard-{shard}#{vnode}"), shard));

        points.Sort((a, b) => a.Position.CompareTo(b.Position));

        _ringPositions = points.Select(p => p.Position).ToArray();
        _ringShards = points.Select(p => p.Shard).ToArray();
    }

    /// <summary>
    /// Ищем на кольце первую точку, чей хеш не меньше хеша ключа, — это и есть
    /// движение «по часовой стрелке». Поиск двоичный, потому что массив
    /// отсортирован.
    /// </summary>
    public int GetShard(int shardKey)
    {
        var keyPosition = ShardHash.Compute(shardKey);

        var index = Array.BinarySearch(_ringPositions, keyPosition);

        if (index < 0)
        {
            // Точного совпадения нет — BinarySearch возвращает дополнение
            // до индекса первого большего элемента.
            index = ~index;
        }

        // Ключ оказался за последней точкой кольца: замыкаем на первую,
        // кольцо ведь круглое.
        if (index >= _ringPositions.Length)
            index = 0;

        return _ringShards[index];
    }
}
