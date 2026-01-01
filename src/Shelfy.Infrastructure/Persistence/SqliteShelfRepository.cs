using Microsoft.Data.Sqlite;
using Shelfy.Core.Domain.Entities;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Shelf の SQLite リポジトリ
/// </summary>
public class SqliteShelfRepository : IShelfRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteShelfRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Shelf?> GetByIdAsync(ShelfId id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, ParentId, SortOrder, IsPinned FROM Shelves WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.Value.ToString());

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapToShelf(reader);
    }

    public async Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, ParentId, SortOrder, IsPinned FROM Shelves ORDER BY SortOrder";

        var shelves = new List<Shelf>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            shelves.Add(MapToShelf(reader));
        }

        return shelves;
    }

    public async Task<IReadOnlyList<Shelf>> GetChildrenAsync(ShelfId? parentId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        if (parentId.HasValue)
        {
            command.CommandText = "SELECT Id, Name, ParentId, SortOrder, IsPinned FROM Shelves WHERE ParentId = @ParentId ORDER BY SortOrder";
            command.Parameters.AddWithValue("@ParentId", parentId.Value.Value.ToString());
        }
        else
        {
            command.CommandText = "SELECT Id, Name, ParentId, SortOrder, IsPinned FROM Shelves WHERE ParentId IS NULL ORDER BY SortOrder";
        }

        var shelves = new List<Shelf>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            shelves.Add(MapToShelf(reader));
        }

        return shelves;
    }

    public async Task AddAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Shelves (Id, Name, ParentId, SortOrder, IsPinned)
            VALUES (@Id, @Name, @ParentId, @SortOrder, @IsPinned)
            """;
        command.Parameters.AddWithValue("@Id", shelf.Id.Value.ToString());
        command.Parameters.AddWithValue("@Name", shelf.Name);
        command.Parameters.AddWithValue("@ParentId", shelf.ParentId.HasValue ? shelf.ParentId.Value.Value.ToString() : DBNull.Value);
        command.Parameters.AddWithValue("@SortOrder", shelf.SortOrder);
        command.Parameters.AddWithValue("@IsPinned", shelf.IsPinned ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Shelf shelf, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Shelves 
            SET Name = @Name, ParentId = @ParentId, SortOrder = @SortOrder, IsPinned = @IsPinned
            WHERE Id = @Id
            """;
        command.Parameters.AddWithValue("@Id", shelf.Id.Value.ToString());
        command.Parameters.AddWithValue("@Name", shelf.Name);
        command.Parameters.AddWithValue("@ParentId", shelf.ParentId.HasValue ? shelf.ParentId.Value.Value.ToString() : DBNull.Value);
        command.Parameters.AddWithValue("@SortOrder", shelf.SortOrder);
        command.Parameters.AddWithValue("@IsPinned", shelf.IsPinned ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(ShelfId id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Shelves WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id.Value.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Shelf MapToShelf(SqliteDataReader reader)
    {
        var id = new ShelfId(Guid.Parse(reader.GetString(0)));
        var name = reader.GetString(1);
        var parentIdStr = reader.IsDBNull(2) ? null : reader.GetString(2);
        var parentId = parentIdStr != null ? new ShelfId(Guid.Parse(parentIdStr)) : (ShelfId?)null;
        var sortOrder = reader.GetInt32(3);
        var isPinned = reader.GetInt32(4) == 1;

        return new Shelf(id, name, parentId, sortOrder, isPinned);
    }
}
