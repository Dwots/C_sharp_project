namespace GameLibApi.Models;

/// <summary>
/// Шаг сетки партиционирования. Определяет и длину интервала одной партиции,
/// и формат её имени.
/// </summary>
public enum PartitionGranularity
{
    Day,
    Month
}

/// <summary>
/// Партиционированная таблица, за которой следит сервис.
/// Настраивается в appsettings, секция Partitioning:Targets.
/// </summary>
public class PartitionTarget
{
    public string Schema { get; set; } = "public";
    public string Table { get; set; } = string.Empty;
    public PartitionGranularity Granularity { get; set; } = PartitionGranularity.Month;

    /// <summary>
    /// На сколько интервалов вперёд должны существовать партиции.
    /// Горизонт в 3 при Granularity=Day означает «на 3 дня вперёд».
    /// </summary>
    public int HorizonUnits { get; set; } = 3;

    public string FullName => $"{Schema}.{Table}";
}

/// <summary>
/// Одна партиция, которую сервис ожидает увидеть.
/// </summary>
public record ExpectedPartition(string Name, DateTime From, DateTime To);

/// <summary>
/// Итог одного запуска создания партиций.
/// </summary>
public class PartitionJobResult
{
    public string Table { get; set; } = string.Empty;
    public int ExistingCount { get; set; }
    public int RequiredCount { get; set; }
    public List<string> Created { get; set; } = new();
    public string? Error { get; set; }

    public int MissingCount => Created.Count;
    public bool Success => Error is null;
}

/// <summary>
/// Итог проверки состояния партиций.
/// </summary>
public class PartitionHealthResult
{
    public string Table { get; set; } = string.Empty;
    public int HorizonUnits { get; set; }
    public List<string> Expected { get; set; } = new();
    public List<string> Missing { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>CRITICAL, если хотя бы одной ожидаемой партиции нет.</summary>
    public bool IsHealthy => Missing.Count == 0;
    public string Status => IsHealthy ? "OK" : "CRITICAL";
}
