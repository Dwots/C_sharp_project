using GameLibApi.Models;

namespace GameLibApi.Services.Interfaces;

public interface IPartitionService
{
    IReadOnlyList<PartitionTarget> Targets { get; }

    PartitionTarget? FindTarget(string table);

    /// <summary>Партиции, которые должны существовать на заданный момент.</summary>
    List<ExpectedPartition> BuildExpected(PartitionTarget target, DateTime now);

    /// <summary>Часть 10: создаёт недостающие партиции. Безопасно при повторном запуске.</summary>
    Task<PartitionJobResult> CreateMissingPartitionsAsync(PartitionTarget target, DateTime now);

    /// <summary>Часть 11: проверяет наличие всех ожидаемых партиций.</summary>
    Task<PartitionHealthResult> CheckHealthAsync(PartitionTarget target, DateTime now);

    Task<List<PartitionHealthResult>> CheckAllAsync(DateTime now);

    /// <summary>Удаление партиции для проверки алертинга.</summary>
    Task DropPartitionAsync(string schema, string partitionName);
}
