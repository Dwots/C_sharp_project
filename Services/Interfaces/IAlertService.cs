using GameLibApi.Models;

namespace GameLibApi.Services.Interfaces;

public interface IAlertService
{
    /// <summary>Настроен ли канал доставки (есть токен и чат).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Обрабатывает результат проверки и отправляет уведомление только при
    /// смене состояния: OK → CRITICAL даёт алерт, CRITICAL → OK — recovery,
    /// повторы одного и того же состояния подавляются.
    /// Возвращает true, если сообщение было отправлено.
    /// </summary>
    Task<bool> ProcessHealthResultAsync(PartitionHealthResult health);

    /// <summary>Отправка произвольного текста.</summary>
    Task<bool> SendAsync(string text);

    /// <summary>Сброс запомненного состояния — для повторных демонстраций.</summary>
    void ResetState();
}
