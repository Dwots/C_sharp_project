using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;

namespace GameLibApi.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IEnumerable<UserResponseDto>> GetAllAsync()
    {
        var users = await _repository.GetAllAsync();
        return users.Select(MapToResponse);
    }

    public async Task<UserResponseDto?> GetByIdAsync(int id, int? currentUserId, string? currentRole)
    {
        // Проверка доступа в сервисном слое!
        // User может смотреть только свой профиль
        if (currentRole == "User" && currentUserId != id)
        {
            _logger.LogWarning("User {CurrentUserId} tried to access user {TargetUserId} profile", 
                currentUserId, id);
            throw new UnauthorizedAccessException("Нет доступа к профилю другого пользователя");
        }

        var user = await _repository.GetByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User with ID {Id} not found", id);
            return null;
        }
        return MapToResponse(user);
    }

    public async Task<UserResponseDto> CreateAsync(CreateUserDto dto)
    {
        // Проверка на дубликаты
        var existingEmail = await _repository.GetByEmailAsync(dto.Email);
        if (existingEmail != null)
        {
            _logger.LogWarning("Attempt to create user with existing email {Email}", dto.Email);
            throw new InvalidOperationException($"Email {dto.Email} уже используется");
        }

        var existingUsername = await _repository.GetByUsernameAsync(dto.Username);
        if (existingUsername != null)
        {
            _logger.LogWarning("Attempt to create user with existing username {Username}", dto.Username);
            throw new InvalidOperationException($"Username {dto.Username} уже занят");
        }

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "User", // По умолчанию User
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(user);
        _logger.LogInformation("User created: {Username} (ID: {Id})", created.Username, created.Id);
        
        return MapToResponse(created);
    }

    public async Task<UserResponseDto?> UpdateAsync(int id, UpdateUserDto dto, int? currentUserId, string? currentRole)
    {
        // Проверка доступа в сервисном слое!
        // User может редактировать только свой профиль
        if (currentRole == "User" && currentUserId != id)
        {
            _logger.LogWarning("User {CurrentUserId} tried to update user {TargetUserId} profile", 
                currentUserId, id);
            throw new UnauthorizedAccessException("Нельзя редактировать профиль другого пользователя");
        }

        // Проверяем, не занят ли email другим пользователем
        var existingByEmail = await _repository.GetByEmailAsync(dto.Email);
        if (existingByEmail != null && existingByEmail.Id != id)
        {
            throw new InvalidOperationException($"Email {dto.Email} уже используется");
        }

        // Проверяем, не занят ли username другим пользователем
        var existingByUsername = await _repository.GetByUsernameAsync(dto.Username);
        if (existingByUsername != null && existingByUsername.Id != id)
        {
            throw new InvalidOperationException($"Username {dto.Username} уже занят");
        }

        var user = new User
        {
            Id = id,
            Username = dto.Username,
            Email = dto.Email
        };

        var updated = await _repository.UpdateAsync(user);
        if (updated == null)
        {
            _logger.LogWarning("User with ID {Id} not found for update", id);
            return null;
        }

        _logger.LogInformation("User updated: {Username} (ID: {Id})", updated.Username, id);
        return MapToResponse(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _repository.DeleteAsync(id);
        if (!result)
        {
            _logger.LogWarning("User with ID {Id} not found for deletion", id);
        }
        else
        {
            _logger.LogInformation("User deleted: ID {Id}", id);
        }
        return result;
    }

    private static UserResponseDto MapToResponse(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            GamesInLibrary = user.UserGames?.Count ?? 0
        };
    }
}
