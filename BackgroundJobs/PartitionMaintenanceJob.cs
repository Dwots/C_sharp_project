using GameLibApi.Services.Interfaces;

namespace GameLibApi.BackgroundJobs;

/// <summary>
/// Фоновая служба обслуживания партиций.
///
/// Делает два дела по расписанию:
///   1. создаёт недостающие партиции (часть 10);
///   2. проверяет состояние и при расхождении шлёт алерт (часть 11).
///
/// Порядок важен: сначала создание, потом проверка. Если job отработала
/// штатно, проверка находит всё на месте и молчит. Алерт приходит только
/// тогда, когда создание по какой-то причине не сработало — то есть именно
/// в той ситуации, ради которой контроль и заводится.
/// </summary>
public class PartitionMaintenanceJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PartitionMaintenanceJob> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public PartitionMaintenanceJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PartitionMaintenanceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = configuration.GetValue("Partitioning:JobEnabled", true);
        _interval = TimeSpan.FromMinutes(configuration.GetValue("Partitioning:IntervalMinutes", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Partition maintenance job отключена настройкой Partitioning:JobEnabled");
            return;
        }

        _logger.LogInformation("Partition maintenance job запущена, интервал {Interval}", _interval);

        // Небольшая задержка при старте: даём приложению и БД подняться.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync();

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Partition maintenance job остановлена");
    }

    private async Task RunOnceAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var partitions = scope.ServiceProvider.GetRequiredService<IPartitionService>();
            var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();

            var now = DateTime.UtcNow;

            foreach (var target in partitions.Targets)
            {
                await partitions.CreateMissingPartitionsAsync(target, now);

                var health = await partitions.CheckHealthAsync(target, now);
                await alerts.ProcessHealthResultAsync(health);
            }
        }
        catch (Exception ex)
        {
            // Фоновая служба не должна падать: следующий тик попробует снова.
            _logger.LogError(ex, "Partition maintenance job: непредвиденная ошибка");
        }
    }
}
