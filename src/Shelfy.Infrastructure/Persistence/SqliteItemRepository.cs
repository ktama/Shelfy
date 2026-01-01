using Microsoft.Data.Sqlite;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Item の SQLite リポジトリ
/// </summary>
public class SqliteItemRepository : IItemRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteItemRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt 
            FROM Items WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", id.Value.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapToItem(reader);
    }

    public async Task<IReadOnlyList<Item>> GetByShelfIdAsync(ShelfId shelfId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt 
            FROM Items WHERE ShelfId = @ShelfId
            """;
        command.Parameters.AddWithValue("@ShelfId", shelfId.Value.ToString());

        var items = new List<Item>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapToItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<Item>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt 
            FROM Items 
            WHERE DisplayName LIKE @Query 
               OR Target LIKE @Query 
               OR Memo LIKE @Query
            """;
        command.Parameters.AddWithValue("@Query", $"%{query}%");

        var items = new List<Item>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapToItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<Item>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt 
            FROM Items 
            WHERE LastAccessedAt IS NOT NULL
            ORDER BY LastAccessedAt DESC
            LIMIT @Count
            """;
        command.Parameters.AddWithValue("@Count", count);

        var items = new List<Item>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapToItem(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<Item>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt 
            FROM Items
            """;

        var items = new List<Item>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapToItem(reader));
        }

        return items;
    }

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Items (Id, ShelfId, Type, Target, DisplayName, Memo, CreatedAt, LastAccessedAt)
            VALUES (@Id, @ShelfId, @Type, @Target, @DisplayName, @Memo, @CreatedAt, @LastAccessedAt)
            """;
        command.Parameters.AddWithValue("@Id", item.Id.Value.ToString());
        command.Parameters.AddWithValue("@ShelfId", item.ShelfId.Value.ToString());
        command.Parameters.AddWithValue("@Type", (int)item.Type);
        command.Parameters.AddWithValue("@Target", item.Target);
        command.Parameters.AddWithValue("@DisplayName", item.DisplayName);
        command.Parameters.AddWithValue("@Memo", item.Memo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@LastAccessedAt", item.LastAccessedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Items 
            SET ShelfId = @ShelfId, Type = @Type, Target = @Target, 
                DisplayName = @DisplayName, Memo = @Memo, 
                CreatedAt = @CreatedAt, LastAccessedAt = @LastAccessedAt
            WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", item.Id.Value.ToString());
        command.Parameters.AddWithValue("@ShelfId", item.ShelfId.Value.ToString());
        command.Parameters.AddWithValue("@Type", (int)item.Type);
        command.Parameters.AddWithValue("@Target", item.Target);
        command.Parameters.AddWithValue("@DisplayName", item.DisplayName);
        command.Parameters.AddWithValue("@Memo", item.Memo ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", item.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@LastAccessedAt", item.LastAccessedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(ItemId id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Items WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.Value.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Item MapToItem(SqliteDataReader reader)
    {
        var id = new ItemId(Guid.Parse(reader.GetString(0)));
        var shelfId = new ShelfId(Guid.Parse(reader.GetString(1)));
        var type = (ItemType)reader.GetInt32(2);
        var target = reader.GetString(3);
        var displayName = reader.GetString(4);
        var memo = reader.IsDBNull(5) ? null : reader.GetString(5);
        var createdAt = DateTime.Parse(reader.GetString(6));
        var lastAccessedAtStr = reader.IsDBNull(7) ? null : reader.GetString(7);
        var lastAccessedAt = lastAccessedAtStr != null ? DateTime.Parse(lastAccessedAtStr) : (DateTime?)null;

        return new Item(id, shelfId, type, target, displayName, memo, createdAt, lastAccessedAt);
    }
}
