using Xunit;
using FluentAssertions;
using GameLibApi.Data;
using GameLibApi.Models;
using GameLibApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GameLibApi.Tests.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new UserRepository(_context);
        SeedData();
    }

    private void SeedData()
    {
        var users = new List<User>
        {
            new() { Id = 1, Username = "admin", Email = "admin@test.com", PasswordHash = "hash1", Role = "Admin" },
            new() { Id = 2, Username = "user1", Email = "user1@test.com", PasswordHash = "hash2", Role = "User" }
        };
        _context.Users.AddRange(users);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        var result = await _repository.GetAllAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUser()
    {
        var result = await _repository.GetByIdAsync(1);
        result.Should().NotBeNull();
        result!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        var result = await _repository.GetByEmailAsync("admin@test.com");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        var result = await _repository.GetByEmailAsync("nonexistent@test.com");
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidUser_ReturnsCreatedUser()
    {
        var newUser = new User { Username = "newuser", Email = "new@test.com", PasswordHash = "hash", Role = "User" };
        var result = await _repository.CreateAsync(newUser);
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteAsync_ExistingUser_ReturnsTrue()
    {
        var result = await _repository.DeleteAsync(1);
        result.Should().BeTrue();
    }
}
