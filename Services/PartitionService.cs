using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

/// <summary>
/// Создание и контроль партиций.
///
/// Ключевая идея: имя партиции однозначно выводится из её интервала
/// (play_sessions_2026_09, events_2026_09_11). Поэтому «какие партиции должны
/// существовать» — это чистое вычисление от текущей даты и горизонта,
/// а «каких не хватает» — разность с тем, что вернул системный каталог.
/// </summary>
public class PartitionService : IPartitionService
{
    private readonly IPartitionRepository _repository;
    private readonly ILogger<PartitionService> _logger;
    private readonly List<PartitionTarget> _targets;

    public PartitionService(
        IPartitionRepository repository,
        IConfiguration configuration,
        ILogger<PartitionService> logger)
    {
        _repository = repository;
        _logger = logger;
        _targets = configuration.GetSection("Partitioning:Targets").Get<List<PartitionTarget>>() ?? new();
    }

    public IReadOnlyList<PartitionTarget> Targets => _targets;

    public PartitionTarget? FindTarget(string table) =>
        _targets.FirstOrDefault(t => string.Equals(t.Table, table, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------
    // Расчёт ожидаемых партиций
    // ------------------------------------------------------------------

    /// <summary>
    /// Партиции, которые должны существовать: текущий интервал плюс HorizonUnits
    /// следующих. Горизонт 3 при шаге в день означает сегодня + 3 дня = 4 партиции.
    /// </summary>
    public List<ExpectedPartition> BuildExpected(PartitionTarget target, DateTime now)
    {
        var result = new List<ExpectedPartition>();
        var start = Truncate(now, target.Granularity);

        for (var i = 0; i <= target.HorizonUnits; i++)
        {
            var from = Advance(start, target.Granularity, i);
            var to = Advance(start, target.Granularity, i + 1);
            result.Add(new ExpectedPartition(BuildName(target, from), from, to));
        }

        return result;
    }

    private static DateTime Truncate(DateTime value, PartitionGranularity granularity) =>
        granularity switch
        {
            PartitionGranularity.Day => value.Date,
            PartitionGranularity.Month => new DateTime(value.Year, value.Month, 1),
            _ => value.Date
        };

    private static DateTime Advance(DateTime start, PartitionGranularity granularity, int steps) =>
        granularity switch
        {
            PartitionGranularity.Day => start.AddDays(steps),
            PartitionGranularity.Month => start.AddMonths(steps),
            _ => start.AddDays(steps)
        };

    /// <summary>
    /// Имя партиции по её начальной дате. Формат совпадает с тем, который
    /// использован в миграции 009, иначе сервис не увидит уже созданные партиции.
    /// </summary>
    private static string BuildName(PartitionTarget target, DateTime from) =>
        target.Granularity switch
        {
            PartitionGranularity.Day => $"{target.Table}_{from:yyyy_MM_dd}",
            PartitionGranularity.Month => $"{target.Table}_{from:yyyy_MM}",
            _ => $"{target.Table}_{from:yyyy_MM_dd}"
        };

    // ------------------------------------------------------------------
    // Часть 10: создание недостающих партиций
    // ------------------------------------------------------------------

    public async Task<PartitionJobResult> CreateMissingPartitionsAsync(PartitionTarget target, DateTime now)
    {
        var result = new PartitionJobResult { Table = target.FullName };

        try
        {
            if (!await _repository.IsPartitionedTableAsync(target.Schema, target.Table))
            {
                result.Error = $"Таблица {target.FullName} не найдена или не партиционирована";
                _logger.LogError("Partition job: {Error}", result.Error);
                return result;
            }

            var existing = await _repository.GetExistingPartitionsAsync(target.Schema, target.Table);
            var expected = BuildExpected(target, now);

            result.ExistingCount = existing.Count;
            result.RequiredCount = expected.Count;

            var missing = expected.Where(e => !existing.Contains(e.Name)).ToList();

            _logger.LogInformation(
                "Partition job started | table={Table} existing={Existing} required={Required} missing={Missing}",
                target.FullName, existing.Count, expected.Count, missing.Count);

            foreach (var partition in missing)
            {
                await _repository.CreatePartitionAsync(
                    target.Schema, target.Table, partition.Name, partition.From, partition.To);
                result.Created.Add(partition.Name);
            }

            _logger.LogInformation(
                "Partition job finished | table={Table} created={Created}",
                target.FullName, result.Created.Count);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            _logger.LogError(ex, "Partition job failed for {Table}", target.FullName);
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Часть 11: проверка состояния
    // ------------------------------------------------------------------

    public async Task<PartitionHealthResult> CheckHealthAsync(PartitionTarget target, DateTime now)
    {
        var expected = BuildExpected(target, now);
        var existing = await _repository.GetExistingPartitionsAsync(target.Schema, target.Table);

        var health = new PartitionHealthResult
        {
            Table = target.FullName,
            HorizonUnits = target.HorizonUnits,
            Expected = expected.Select(e => e.Name).ToList(),
            Missing = expected.Where(e => !existing.Contains(e.Name)).Select(e => e.Name).ToList(),
            CheckedAt = now
        };

        if (health.IsHealthy)
            _logger.LogInformation("Partition check OK | table={Table}", target.FullName);
        else
            _logger.LogError("Partition check CRITICAL | table={Table} missing={Missing}",
                target.FullName, string.Join(", ", health.Missing));

        return health;
    }

    public async Task<List<PartitionHealthResult>> CheckAllAsync(DateTime now)
    {
        var results = new List<PartitionHealthResult>();
        foreach (var target in _targets)
            results.Add(await CheckHealthAsync(target, now));
        return results;
    }

    /// <summary>Удаление партиции — только для проверки алертинга.</summary>
    public Task DropPartitionAsync(string schema, string partitionName) =>
        _repository.DropPartitionAsync(schema, partitionName);
}
