using GameLibApi.DTOs;

namespace GameLibApi.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> RegisterAsync(RegisterWithRoleDto dto);
    string HashApiKey(string apiKey);
}