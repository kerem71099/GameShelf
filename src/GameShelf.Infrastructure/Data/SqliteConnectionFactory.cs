using Microsoft.Data.Sqlite;
using System.Data;

namespace GameShelf.Infrastructure.Data;

public interface ISqliteConnectionFactory
{
    IDbConnection Create();
}

/// <summary>
/// SQLite bağlantı üreticisi (WAL + foreign keys açık).
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory, IDisposable
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();

        // WAL: okuma yazma eşzamanlılığı ve daha az kilitleme.
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        command.ExecuteNonQuery();
    }

    public IDbConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.SuppressFinalize(this);
    }
}
