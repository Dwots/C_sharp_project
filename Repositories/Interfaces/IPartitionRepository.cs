namespace GameLibApi.Repositories.Interfaces;

public interface IPartitionRepository
{
    /// <summary>Имена существующих партиций таблицы.</summary>
    Task<HashSet<string>> GetExistingPartitionsAsync(string schema, string table);

    /// <summary>Существует ли таблица и партиционирована ли она.</summary>
    Task<bool> IsPartitionedTableAsync(string schema, string table);

    /// <summary>Создаёт партицию за интервал [from, to). Безопасно при повторе.</summary>
    Task CreatePartitionAsync(string schema, string table, string partitionName, DateTime from, DateTime to);

    /// <summary>Удаляет партицию. Используется для проверки алертинга.</summary>
    Task DropPartitionAsync(string schema, string partitionName);
}
