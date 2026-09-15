namespace GameLibApi.Services.Sharding;

/// <summary>
/// Router — компонент, отвечающий на единственный вопрос:
/// «на каком шарде лежит запись с таким ключом?».
///
/// Интерфейс один, реализаций две: hash(key) % N и Consistent Hashing.
/// Это позволяет сравнить стратегии на одних и тех же данных, подменив
/// только реализацию.
/// </summary>
public interface IShardRouter
{
    /// <summary>Название стратегии — для отчётов и логов.</summary>
    string Strategy { get; }

    /// <summary>Сколько шардов сейчас в кольце.</summary>
    int ShardCount { get; }

    /// <summary>Номер шарда для указанного ключа шардирования.</summary>
    int GetShard(int shardKey);
}
