using Microsoft.EntityFrameworkCore;
using GameLibApi.Models;

namespace GameLibApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Виртуальные коллекции для таблиц( = таблицы в базе данных)
    public DbSet<Game> Games { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<GameCategory> GameCategories { get; set; }
    public DbSet<UserGame> UserGames { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Составной первичный ключ для many-to-many
        modelBuilder.Entity<GameCategory>()
            .HasKey(gc => new { gc.GameId, gc.CategoryId });

        modelBuilder.Entity<UserGame>()
            .HasKey(ug => new { ug.UserId, ug.GameId });

        // Ограничение рейтинга 1-10
        modelBuilder.Entity<UserGame>()
            .Property(ug => ug.Rating)
            .HasAnnotation("CheckConstraint", "rating >= 1 AND rating <= 10");
    }
}