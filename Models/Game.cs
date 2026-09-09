using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameLibApi.Models;

[Table("games")]
public class Game
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("title")]
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("price")]
    public decimal Price { get; set; }

    [Column("developer")]
    [MaxLength(200)]
    public string? Developer { get; set; }

    [Column("release_date")]
    public DateOnly? ReleaseDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<GameCategory> GameCategories { get; set; } = new();
}