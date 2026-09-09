using Xunit;
using FluentAssertions;
using GameLibApi.Data;
using GameLibApi.Models;
using GameLibApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameLibApi.Tests.Repositories;

public class CategoryRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CategoryRepository _repository;

    public CategoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new CategoryRepository(_context);
        SeedData();
    }

    private void SeedData()
    {
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Action", Description = "Action games" },
            new() { Id = 2, Name = "RPG", Description = "Role-playing games" },
            new() { Id = 3, Name = "Strategy", Description = "Strategy games" }
        };
        _context.Categories.AddRange(categories);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsAllCategories()
    {
        var result = await _repository.GetAllAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCategory()
    {
        var result = await _repository.GetByIdAsync(1);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Action");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidCategory_ReturnsCreatedCategory()
    {
        var newCategory = new Category { Name = "Horror", Description = "Horror games" };
        var result = await _repository.CreateAsync(newCategory);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteAsync_ExistingCategory_ReturnsTrue()
    {
        var result = await _repository.DeleteAsync(1);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingCategory_ReturnsFalse()
    {
        var result = await _repository.DeleteAsync(999);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ExistingCategory_ReturnsTrue()
    {
        var result = await _repository.ExistsAsync(1);
        result.Should().BeTrue();
    }
}
