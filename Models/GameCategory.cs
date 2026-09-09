using System.ComponentModel.DataAnnotations.Schema;

namespace GameLibApi.Models;

[Table("game_categories")]
public class GameCategory
{
    [Column("game_id")]
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Column("category_id")]
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}