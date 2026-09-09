using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GameLibApi.DTOs;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;
using GameLibApi.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace GameLibApi.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly IMetricsService _metrics;

    public AuthService(
        IUserRepository userRepository,
        IConfiguration config,
        ILogger<AuthService> logger,
        IMetricsService metrics)
    {
        _userRepository = userRepository;
        _config = config;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: user {Email} not found", dto.Email);
            _metrics.IncrementLoginAttempts(false);
            throw new UnauthorizedAccessException("Неверный email или пароль");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: wrong password for {Email}", dto.Email);
            _metrics.IncrementLoginAttempts(false);
            throw new UnauthorizedAccessException("Неверный email или пароль");
        }

        var expiresAt = DateTime.UtcNow.AddHours(24);
        var token = GenerateJwtToken(user, expiresAt);

        _logger.LogInformation("User {Username} ({Email}) logged in successfully", user.Username, user.Email);
        _metrics.IncrementLoginAttempts(true);

        return new AuthResponseDto
        {
            Token = token,
            Username = user.Username,
            Role = user.Role,
            ExpiresAt = expiresAt
        };
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterWithRoleDto dto)
    {
        if (await _userRepository.GetByEmailAsync(dto.Email) != null)
            throw new InvalidOperationException("Email уже используется");

        if (await _userRepository.GetByUsernameAsync(dto.Username) != null)
            throw new InvalidOperationException("Username уже занят");

        var validRoles = new[] { "Admin", "Manager", "User" };
        if (!validRoles.Contains(dto.Role))
            throw new ArgumentException("Недопустимая роль");

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user);

        _metrics.IncrementUserRegistrations();

        var expiresAt = DateTime.UtcNow.AddHours(24);
        var token = GenerateJwtToken(created, expiresAt);

        _logger.LogInformation("New user registered: {Username} ({Email}) with role {Role}", 
            created.Username, created.Email, created.Role);
        
        return new AuthResponseDto
        {
            Token = token,
            Username = created.Username,
            Role = created.Role,
            ExpiresAt = expiresAt
        };
    }

    private string GenerateJwtToken(User user, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("permissions", GetPermissions(user.Role))
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["AuthSettings:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "GameLibApi",
            audience: "GameLibApi",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GetPermissions(string role)
    {
        return role switch
        {
            "Admin" => "read,create,update,delete",
            "Manager" => "read,create,update",
            "User" => "read",
            _ => "read"
        };
    }

    public string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}