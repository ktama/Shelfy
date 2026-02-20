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
        _connectionString = $"Data Source={databasePath};Cache=Shared";
    }

    public SqliteConnection CreateConnection()
    {
        EnsureInitialized();
        var connection = new SqliteConnection(_connectionString);
        // 接続ごとに外部キー制約を有効化（SQLite では接続単位の設定）
        connection.StateChange += (s, e) =>
        {
            if (e.CurrentState == global::System.Data.ConnectionState.Open && s is SqliteConnection conn)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }
        };
        return connection;
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
        // 外部キー制約を有効化
        using var pragmaFk = connection.CreateCommand();
        pragmaFk.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaFk.ExecuteNonQuery();

        // WAL モードを有効化（並行読み取り性能向上）
        using var pragmaWal = connection.CreateCommand();
        pragmaWal.CommandText = "PRAGMA journal_mode = WAL;";
        pragmaWal.ExecuteNonQuery();

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
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LastAccessedAt TEXT NULL,
                FOREIGN KEY (ShelfId) REFERENCES Shelves(Id) ON DELETE CASCADE
            );
            """;

        const string createSettingsTable = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;

        const string createIndexes = """
            CREATE INDEX IF NOT EXISTS IX_Shelves_ParentId ON Shelves(ParentId);
            CREATE INDEX IF NOT EXISTS IX_Items_ShelfId ON Items(ShelfId);
            CREATE INDEX IF NOT EXISTS IX_Items_DisplayName ON Items(DisplayName);
            CREATE INDEX IF NOT EXISTS IX_Items_Target ON Items(Target);
            """;

        using var command = connection.CreateCommand();
        command.CommandText = $"{createShelvesTable}\n{createItemsTable}\n{createSettingsTable}\n{createIndexes}";
        command.ExecuteNonQuery();

        // バージョンベースのマイグレーション
        MigrateSchema(connection);
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetSchemaVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void MigrateSchema(SqliteConnection connection)
    {
        var currentVersion = GetSchemaVersion(connection);

        // Version 0 → 1: Items テーブルに SortOrder カラムがない場合は追加
        if (currentVersion < 1)
        {
            using var pragmaCmd = connection.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA table_info(Items);";
            using var reader = pragmaCmd.ExecuteReader();

            var hasSortOrder = false;
            while (reader.Read())
            {
                if (reader.GetString(1) == "SortOrder")
                {
                    hasSortOrder = true;
                    break;
                }
            }
            reader.Close();

            if (!hasSortOrder)
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE Items ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;";
                alterCmd.ExecuteNonQuery();
            }

            SetSchemaVersion(connection, 1);
        }

        // 今後のマイグレーションはここに追加:
        // if (currentVersion < 2) { ... SetSchemaVersion(connection, 2); }
    }

    public void Dispose()
    {
        // Nothing to dispose for SQLite connection factory
        GC.SuppressFinalize(this);
    }
}
