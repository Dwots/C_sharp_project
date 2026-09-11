using System.Globalization;
using Dapper;
using Npgsql;
using GameLibApi.Repositories.Interfaces;

namespace GameLibApi.Repositories;

/// <summary>
/// Доступ к метаданным партиций. Работает на Dapper явным SQL:
/// все запросы к системным каталогам должны быть видимы и проверяемы вручную.
/// </summary>
public class PartitionRepository : IPartitionRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PartitionRepository> _logger;

    public PartitionRepository(IConfiguration configuration, ILogger<PartitionRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Имена существующих партиций таблицы, по данным системного каталога.
    /// pg_inherits связывает родительскую таблицу с дочерними — именно так
    /// PostgreSQL хранит принадлежность партиций.
    /// </summary>
    public async Task<HashSet<string>> GetExistingPartitionsAsync(string schema, string table)
    {
        const string sql = @"
            SELECT child.relname
            FROM pg_inherits i
            JOIN pg_class  parent   ON parent.oid = i.inhparent
            JOIN pg_class  child    ON child.oid  = i.inhrelid
            JOIN pg_namespace ns    ON ns.oid     = parent.relnamespace
            WHERE ns.nspname = @Schema
              AND parent.relname = @Table";

        await using var connection = CreateConnection();
        var names = await connection.QueryAsync<string>(sql, new { Schema = schema, Table = table });

        return names.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Существует ли сама партиционированная таблица и является ли она
    /// действительно партиционированной (relkind = 'p').
    /// </summary>
    public async Task<bool> IsPartitionedTableAsync(string schema, string table)
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1 FROM pg_class c
                JOIN pg_namespace ns ON ns.oid = c.relnamespace
                WHERE ns.nspname = @Schema AND c.relname = @Table AND c.relkind = 'p')";

        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(sql, new { Schema = schema, Table = table });
    }

    /// <summary>
    /// Создаёт партицию за интервал [from, to).
    /// IF NOT EXISTS делает операцию безопасной при повторном запуске —
    /// требование идемпотентности из задания.
    ///
    /// Риска инъекции нет: имя собирается из конфигурации и даты, а границы
    /// форматируются из DateTime по фиксированному шаблону, то есть могут
    /// содержать только цифры, дефисы, двоеточия и пробел.
    /// </summary>
    public async Task CreatePartitionAsync(
        string schema, string table, string partitionName, DateTime from, DateTime to)
    {
        var fromLiteral = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var toLiteral = to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {Quote(schema)}.{Quote(partitionName)}
            PARTITION OF {Quote(schema)}.{Quote(table)}
            FOR VALUES FROM ('{fromLiteral}') TO ('{toLiteral}')";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql);

        _logger.LogInformation("Partition created: {Schema}.{Name} [{From:yyyy-MM-dd} .. {To:yyyy-MM-dd})",
            schema, partitionName, from, to);
    }

    /// <summary>
    /// Удаление партиции. Нужно для проверки алертинга: задание требует
    /// искусственно создать сбой и убедиться, что он обнаружен.
    /// </summary>
    public async Task DropPartitionAsync(string schema, string partitionName)
    {
        var sql = $"DROP TABLE IF EXISTS {Quote(schema)}.{Quote(partitionName)}";

        await using var connection = CreateConnection();
        await connection.ExecuteAsync(sql);

        _logger.LogWarning("Partition dropped: {Schema}.{Name}", schema, partitionName);
    }

    /// <summary>
    /// Экранирование идентификатора двойными кавычками с удвоением внутренних.
    /// </summary>
    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
