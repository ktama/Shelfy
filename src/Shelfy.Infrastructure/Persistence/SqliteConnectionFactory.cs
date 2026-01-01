using Microsoft.Data.Sqlite;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// SQLite データベース接続ファクトリ
/// </summary>
public class SqliteConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly object _lock = new();

    public SqliteConnectionFactory(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection CreateConnection()
    {
        EnsureInitialized();
        return new SqliteConnection(_connectionString);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            InitializeSchema(connection);
            _initialized = true;
        }
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        const string createShelvesTable = """
            CREATE TABLE IF NOT EXISTS Shelves (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentId TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (ParentId) REFERENCES Shelves(Id) ON DELETE CASCADE
            );
            """;

        const string createItemsTable = """
            CREATE TABLE IF NOT EXISTS Items (
                Id TEXT PRIMARY KEY,
                ShelfId TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Target TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Memo TEXT NULL,
                CreatedAt TEXT NOT NULL,
                LastAccessedAt TEXT NULL,
                FOREIGN KEY (ShelfId) REFERENCES Shelves(Id) ON DELETE CASCADE
            );
            """;

        const string createIndexes = """
            CREATE INDEX IF NOT EXISTS IX_Shelves_ParentId ON Shelves(ParentId);
            CREATE INDEX IF NOT EXISTS IX_Items_ShelfId ON Items(ShelfId);
            CREATE INDEX IF NOT EXISTS IX_Items_DisplayName ON Items(DisplayName);
            CREATE INDEX IF NOT EXISTS IX_Items_Target ON Items(Target);
            """;

        using var command = connection.CreateCommand();
        command.CommandText = $"{createShelvesTable}\n{createItemsTable}\n{createIndexes}";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Nothing to dispose for SQLite connection factory
        GC.SuppressFinalize(this);
    }
}
