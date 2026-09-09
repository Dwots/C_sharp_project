using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameLibApi.Models;

/// <summary>
/// Игровая сессия — основная растущая сущность сервиса (scaling entity).
/// </summary>
[Table("play_sessions")]
public class PlaySession
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    [Column("game_id")]
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Column("duration_minutes")]
    public int DurationMinutes { get; set; }

    [Column("platform")]
    [MaxLength(20)]
    public string Platform { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
