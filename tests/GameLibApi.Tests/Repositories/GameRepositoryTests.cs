using Xunit;
using FluentAssertions;
using GameLibApi.Data;
using GameLibApi.Models;
using GameLibApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameLibApi.Tests.Repositories;

public class GameRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly GameRepository _repository;

    public GameRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new GameRepository(_context);
        SeedData();
    }

    private void SeedData()
    {
        var games = new List<Game>
        {
            new() { Id = 1, Title = "The Witcher 3", Price = 29.99m, Developer = "CD Projekt Red" },
            new() { Id = 2, Title = "Cyberpunk 2077", Price = 59.99m, Developer = "CD Projekt Red" },
            new() { Id = 3, Title = "GTA V", Price = 19.99m, Developer = "Rockstar" }
        };
        _context.Games.AddRange(games);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsAllGames()
    {
        var result = await _repository.GetAllAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsGame()
    {
        var result = await _repository.GetByIdAsync(1);
        result.Should().NotBeNull();
        result!.Title.Should().Be("The Witcher 3");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidGame_ReturnsCreatedGame()
    {
        var newGame = new Game { Title = "New Game", Price = 49.99m, Developer = "Test Dev" };
        var result = await _repository.CreateAsync(newGame);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteAsync_ExistingGame_ReturnsTrue()
    {
        var result = await _repository.DeleteAsync(1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingGame_ReturnsFalse()
    {
        var result = await _repository.DeleteAsync(999);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ExistingGame_ReturnsTrue()
    {
        var result = await _repository.ExistsAsync(1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingGame_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync(999);
        result.Should().BeFalse();
    }
}
