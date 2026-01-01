using Microsoft.Data.Sqlite;
using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Settings の SQLite リポジトリ
/// </summary>
public class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
        EnsureTableExists();
    }

    private void EnsureTableExists()
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT Value FROM Settings WHERE Key = @Key";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO Settings (Key, Value) VALUES (@Key, @Value)
            ON CONFLICT(Key) DO UPDATE SET Value = @Value;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Key", key);
        command.Parameters.AddWithValue("@Value", value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM Settings WHERE Key = @Key";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Key", key);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT Key, Value FROM Settings";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = new Dictionary<string, string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            result[key] = value;
        }

        return result;
    }
}
