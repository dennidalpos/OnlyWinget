using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OnlyWinget.Infrastructure.Storage.Sqlite;

internal static class WorkspaceSchemaInitializer
{
    private const int CurrentVersion = 1;

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core model is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "EF Core model is defined statically.")]
    public static async Task InitializeAsync(string dbPath, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(dbPath, SqliteOpenMode.ReadWriteCreate);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA synchronous=FULL;", cancellationToken).ConfigureAwait(false);

        // Reserve the writer so concurrent stores initialize a new database only once.
        using var transaction = connection.BeginTransaction(deferred: false);
        var version = Convert.ToInt32(await ScalarAsync(connection, transaction, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false));
        var schemaObjectCount = Convert.ToInt32(await ScalarAsync(connection, transaction,
            "SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';", cancellationToken).ConfigureAwait(false));

        if (schemaObjectCount == 0 && version == 0)
        {
            await using var context = new WorkspaceDbContext(dbPath);
            await ExecuteAsync(connection, transaction, context.Database.GenerateCreateScript(), cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, $"PRAGMA user_version={CurrentVersion};", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return;
        }

        if (version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported workspace schema version {version}; this application supports only version {CurrentVersion}. The database was not modified: no conversion, reset or deletion is performed.");
        }

        await ValidateColumnsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        await VerifyDatabaseAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = mode,
        Cache = SqliteCacheMode.Private,
        Pooling = false,
        ForeignKeys = true
    }.ToString());

    private static async Task ValidateColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var expectedColumns = new Dictionary<string, string[]>
        {
            ["Presets"] = ["Id", "Name"],
            ["PresetItems"] = ["Id", "PresetId", "PackageId", "Source"],
            ["WorkspaceMetadata"] = ["Key", "Value"]
        };

        foreach (var (table, expected) in expectedColumns)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA table_xinfo('{table}');";
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var columns = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString(1);
                var primaryKey = name == (table == "WorkspaceMetadata" ? "Key" : "Id") ? 1L : 0L;
                if (!string.Equals(reader.GetString(2), "TEXT", StringComparison.OrdinalIgnoreCase)
                    || reader.GetInt64(3) != 1 || reader.GetInt64(5) != primaryKey || reader.GetInt64(6) != 0)
                {
                    throw new InvalidOperationException($"Unrecognized workspace column '{table}.{name}'; no schema changes were committed.");
                }
                columns.Add(name);
            }
            if (!columns.SetEquals(expected))
            {
                throw new InvalidOperationException($"Unrecognized workspace table '{table}' for the current schema; no schema changes were committed.");
            }
        }

        var uniqueNames = await ScalarAsync(connection, transaction, """
            SELECT COUNT(*) FROM pragma_index_list('Presets') AS indexes
            WHERE indexes.[unique]=1 AND indexes.partial=0
              AND (SELECT COUNT(*) FROM pragma_index_info(indexes.name))=1
              AND (SELECT name FROM pragma_index_info(indexes.name))='Name';
            """, cancellationToken).ConfigureAwait(false);
        var foreignKeys = await ScalarAsync(connection, transaction, """
            SELECT COUNT(*) FROM pragma_foreign_key_list('PresetItems')
            WHERE "table"='Presets' AND "from"='PresetId' AND "to"='Id' AND on_delete='CASCADE' AND seq=0;
            """, cancellationToken).ConfigureAwait(false);
        var totalForeignKeys = await ScalarAsync(connection, transaction,
            "SELECT COUNT(*) FROM pragma_foreign_key_list('PresetItems');", cancellationToken).ConfigureAwait(false);
        if (Convert.ToInt64(uniqueNames) == 0 || Convert.ToInt64(foreignKeys) != 1 || Convert.ToInt64(totalForeignKeys) != 1)
        {
            throw new InvalidOperationException("Unrecognized workspace uniqueness or foreign-key constraints; no schema changes were committed.");
        }
    }

    private static async Task VerifyDatabaseAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        var result = await ScalarAsync(connection, transaction, "PRAGMA integrity_check;", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(result?.ToString(), "ok", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Workspace database integrity check failed; no schema changes were committed.");
        }
        if (await ScalarAsync(connection, transaction, "PRAGMA foreign_key_check;", cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Workspace database contains invalid foreign keys; no schema changes were committed.");
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
