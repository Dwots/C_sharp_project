namespace GameLibApi.DTOs;

public class CreatePlaySessionDto
{
    public int GameId { get; set; }
    public int DurationMinutes { get; set; }
    public string Platform { get; set; } = string.Empty;
}

/// <summary>
/// Строка ответа для Query 1 и Query 3: сессия вместе с названием игры.
/// </summary>
public class PlaySessionResponseDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Параметры фильтрации истории сессий пользователя.
/// </summary>
public class PlaySessionFilterRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > 100 => 100,  // Максимум 100
            _ => value
        };
    }

    public string? Platform { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? MinDuration { get; set; }
}

/// <summary>
/// Строка агрегата: сколько и сколько времени играли в игру.
/// </summary>
public class GameStatsDto
{
    public int GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string? Categories { get; set; }
    public long SessionsCount { get; set; }
    public long UniquePlayers { get; set; }
    public long TotalMinutes { get; set; }
    public double AvgMinutes { get; set; }
}

/// <summary>
/// Строка агрегата: активность одного пользователя.
/// </summary>
public class UserActivityDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public long SessionsCount { get; set; }
    public long DistinctGames { get; set; }
    public long TotalMinutes { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}
