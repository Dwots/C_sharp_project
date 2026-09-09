using GameLibApi.DTOs;

namespace GameLibApi.Services.Interfaces;

public interface IGameService
{
    Task<IEnumerable<GameResponseDto>> GetAllAsync();
    Task<GameResponseDto?> GetByIdAsync(int id);
    Task<GameResponseDto> CreateAsync(CreateGameDto dto);
    Task<GameResponseDto?> UpdateAsync(int id, UpdateGameDto dto);
    Task<bool> DeleteAsync(int id);
    Task<PagedResponse<GameResponseDto>> GetPagedAsync(GameFilterRequest request);
}