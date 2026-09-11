using System.Text;
using System.Text.Json;
using GameLibApi.Models;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

/// <summary>
/// Отправка алертов о состоянии партиций в Telegram.
///
/// Логика подавления повторов (дополнительное задание части 11):
/// уведомление отправляется не на каждую проверку, а только на **смену
/// состояния**. Переход OK → CRITICAL даёт алерт, последующие CRITICAL
/// молчат, а переход CRITICAL → OK даёт recovery-уведомление.
///
/// Состояние хранится в памяти процесса. Для учебного проекта этого достаточно;
/// в реальной системе при нескольких экземплярах сервиса его пришлось бы
/// вынести в общее хранилище — иначе каждый экземпляр отправит свой алерт.
/// </summary>
public class TelegramAlertService : IAlertService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramAlertService> _logger;
    private readonly string? _botToken;
    private readonly string? _chatId;

    /// <summary>Последнее отправленное состояние по каждой таблице.</summary>
    private readonly Dictionary<string, bool> _lastKnownHealthy = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TelegramAlertService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramAlertService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _botToken = configuration["Alerting:Telegram:BotToken"];
        _chatId = configuration["Alerting:Telegram:ChatId"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken) && !string.IsNullOrWhiteSpace(_chatId);

    /// <summary>
    /// Обрабатывает результат проверки: решает, нужно ли уведомление,
    /// и отправляет его. Возвращает true, если сообщение было отправлено.
    /// </summary>
    public async Task<bool> ProcessHealthResultAsync(PartitionHealthResult health)
    {
        await _lock.WaitAsync();
        try
        {
            var wasHealthy = _lastKnownHealthy.TryGetValue(health.Table, out var previous) ? previous : true;

            // Состояние не изменилось — повторное уведомление не отправляем.
            if (wasHealthy == health.IsHealthy)
            {
                if (!health.IsHealthy)
                    _logger.LogInformation(
                        "Partition alert suppressed (состояние не изменилось) | table={Table}", health.Table);
                return false;
            }

            _lastKnownHealthy[health.Table] = health.IsHealthy;

            var text = health.IsHealthy ? BuildRecoveryMessage(health) : BuildAlertMessage(health);
            return await SendAsync(text);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Сбрасывает запомненное состояние — для повторных прогонов демонстрации.</summary>
    public void ResetState() => _lastKnownHealthy.Clear();

    public async Task<bool> SendAsync(string text)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Telegram не настроен (Alerting:Telegram:*). Сообщение только в лог:\n{Text}", text);
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var payload = JsonSerializer.Serialize(new { chat_id = _chatId, text });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"https://api.telegram.org/bot{_botToken}/sendMessage", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram alert отправлен");
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Telegram вернул {Status}: {Body}", (int)response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            // Сбой доставки уведомления не должен ронять сервис.
            _logger.LogError(ex, "Не удалось отправить Telegram alert");
            return false;
        }
    }

    private static string BuildAlertMessage(PartitionHealthResult health) =>
        $"""
        🚨 Partition alert

        Table: {health.Table}
        Missing partitions:
        {string.Join("\n", health.Missing)}

        Expected horizon: {health.HorizonUnits}

        Checked at:
        {health.CheckedAt:yyyy-MM-dd HH:mm:ss}
        """;

    private static string BuildRecoveryMessage(PartitionHealthResult health) =>
        $"""
        🟢 Partition check OK

        Table: {health.Table}

        All required partitions exist.

        Checked at:
        {health.CheckedAt:yyyy-MM-dd HH:mm:ss}
        """;
}
