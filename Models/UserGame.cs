using System.ComponentModel.DataAnnotations.Schema;

namespace GameLibApi.Models;

[Table("user_games")]
public class UserGame
{
    [Column("user_id")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Column("game_id")]
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Column("rating")]
    public int? Rating { get; set; }  // Оценка от 1 до 10

    [Column("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}