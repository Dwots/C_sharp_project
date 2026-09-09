namespace GameLibApi.DTOs;

public class AddGameToLibraryDto
{
    public int GameId { get; set; }
    public int? Rating { get; set; } // Оценка 1-10
}

public class UpdateUserGameDto
{
    public int? Rating { get; set; }
}

public class UserGameResponseDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int GameId { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public DateTime AddedAt { get; set; }
}