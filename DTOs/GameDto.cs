namespace GameLibApi.DTOs;

public class CreateGameDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Developer { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public List<int>? CategoryIds { get; set; }
}

public class UpdateGameDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Developer { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public List<int>? CategoryIds { get; set; }
}

public class GameResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Developer { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Categories { get; set; } = new();
    
}