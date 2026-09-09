using GameLibApi.DTOs;

namespace GameLibApi.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserResponseDto>> GetAllAsync();
    Task<UserResponseDto?> GetByIdAsync(int id, int? currentUserId, string? currentRole);
    Task<UserResponseDto> CreateAsync(CreateUserDto dto);
    Task<UserResponseDto?> UpdateAsync(int id, UpdateUserDto dto, int? currentUserId, string? currentRole);
    Task<bool> DeleteAsync(int id);
}
