using System.Data;
using Dapper;
using Npgsql;
using GameLibApi.Models;
using GameLibApi.Repositories.Interfaces;

namespace GameLibApi.Repositories;

/// <summary>
/// Репозиторий категорий на Dapper с ручным управлением транзакциями
/// </summary>
public class CategoryDapperRepository : ICategoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CategoryDapperRepository> _logger;

    public CategoryDapperRepository(
        IConfiguration configuration,
        ILogger<CategoryDapperRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт новое подключение к БД
    /// </summary>
    private NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        const string sql = @"
            SELECT 
                c.id AS Id,
                c.name AS Name,
                c.description AS Description
            FROM categories c
            ORDER BY c.name";

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        // Даже для SELECT используем транзакцию (как требуется в задании)
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var categories = await connection.QueryAsync<Category>(sql, transaction: transaction);
            
            // Для каждой категории получаем количество игр
            const string countSql = @"
                SELECT COUNT(*) FROM game_categories WHERE category_id = @CategoryId";

            var result = categories.ToList();
            foreach (var category in result)
            {
                var count = await connection.ExecuteScalarAsync<int>(
                    countSql, 
                    new { CategoryId = category.Id }, 
                    transaction: transaction);
                
                // Инициализируем список для подсчёта
                category.GameCategories = Enumerable.Range(0, count)
                    .Select(_ => new GameCategory())
                    .ToList();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("[Dapper] Retrieved {Count} categories", result.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error retrieving categories");
            throw;
        }
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT 
                id AS Id,
                name AS Name,
                description AS Description
            FROM categories
            WHERE id = @Id";

        const string countSql = @"
            SELECT COUNT(*) FROM game_categories WHERE category_id = @Id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var category = await connection.QueryFirstOrDefaultAsync<Category>(
                sql, 
                new { Id = id }, 
                transaction: transaction);

            if (category != null)
            {
                var count = await connection.ExecuteScalarAsync<int>(
                    countSql, 
                    new { Id = id }, 
                    transaction: transaction);
                
                category.GameCategories = Enumerable.Range(0, count)
                    .Select(_ => new GameCategory())
                    .ToList();
            }

            await transaction.CommitAsync();
            _logger.LogInformation("[Dapper] Retrieved category {Id}", id);
            
            return category;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error retrieving category {Id}", id);
            throw;
        }
    }

    public async Task<Category> CreateAsync(Category category)
    {
        const string sql = @"
            INSERT INTO categories (name, description)
            VALUES (@Name, @Description)
            RETURNING id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var id = await connection.ExecuteScalarAsync<int>(sql, new
            {
                category.Name,
                category.Description
            }, transaction: transaction);

            category.Id = id;

            await transaction.CommitAsync();
            _logger.LogInformation("[Dapper] Created category with ID {Id}", id);
            
            return category;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error creating category");
            throw;
        }
    }

    public async Task<Category?> UpdateAsync(Category category)
    {
        const string checkSql = "SELECT COUNT(*) FROM categories WHERE id = @Id";
        
        const string updateSql = @"
            UPDATE categories 
            SET name = @Name, 
                description = @Description
            WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Проверяем существование
            var exists = await connection.ExecuteScalarAsync<int>(
                checkSql, 
                new { category.Id }, 
                transaction: transaction);

            if (exists == 0)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("[Dapper] Category {Id} not found for update", category.Id);
                return null;
            }

            await connection.ExecuteAsync(updateSql, new
            {
                category.Id,
                category.Name,
                category.Description
            }, transaction: transaction);

            await transaction.CommitAsync();
            _logger.LogInformation("[Dapper] Updated category {Id}", category.Id);
            
            return category;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error updating category {Id}", category.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Сначала удаляем связи, потом категорию (в одной транзакции)
        const string deleteGameCategoriesSql = @"
            DELETE FROM game_categories WHERE category_id = @Id";
        
        const string deleteCategorySql = @"
            DELETE FROM categories WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Удаляем связи many-to-many
            await connection.ExecuteAsync(
                deleteGameCategoriesSql, 
                new { Id = id }, 
                transaction: transaction);

            // Удаляем категорию
            var rowsAffected = await connection.ExecuteAsync(
                deleteCategorySql, 
                new { Id = id }, 
                transaction: transaction);

            await transaction.CommitAsync();
            
            var success = rowsAffected > 0;
            _logger.LogInformation("[Dapper] Delete category {Id}: {Result}", id, success ? "success" : "not found");
            
            return success;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error deleting category {Id}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int id)
    {
        const string sql = "SELECT COUNT(*) FROM categories WHERE id = @Id";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var count = await connection.ExecuteScalarAsync<int>(
                sql, 
                new { Id = id }, 
                transaction: transaction);

            await transaction.CommitAsync();
            
            return count > 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "[Dapper] Error checking category existence {Id}", id);
            throw;
        }
    }
}