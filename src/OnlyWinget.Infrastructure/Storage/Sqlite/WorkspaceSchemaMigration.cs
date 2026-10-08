using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OnlyWinget.Infrastructure.Storage.Sqlite;

internal static class WorkspaceSchemaMigration
{
    private const int CurrentVersion = 1;

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core model is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "EF Core model is defined statically.")]
    public static async Task<string?> InitializeAsync(string dbPath, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection(dbPath, SqliteOpenMode.ReadWriteCreate);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA synchronous=FULL;", cancellationToken).ConfigureAwait(false);

        // Reserve the writer before inspecting or backing up the previous schema.
        using var transaction = connection.BeginTransaction(deferred: false);
        var version = Convert.ToInt32(await ScalarAsync(connection, transaction, "PRAGMA user_version;", cancellationToken).ConfigureAwait(false));
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported workspace schema version {version}; this application supports version {CurrentVersion}. Use a compatible application before saving.");
        }

        var schemaObjectCount = Convert.ToInt32(await ScalarAsync(connection, transaction,
            "SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';", cancellationToken).ConfigureAwait(false));
        if (schemaObjectCount == 0 && version == 0)
        {
            await using var context = new WorkspaceDbContext(dbPath);
            await ExecuteAsync(connection, transaction, context.Database.GenerateCreateScript(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ValidateColumnsAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
            if (version == CurrentVersion) return null;
        }

        string? backupPath = null;
        if (schemaObjectCount != 0)
        {
            await VerifyDatabaseAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            backupPath = await CreateBackupAsync(dbPath, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (backupPath is not null)
            {
                foreach (var statement in new[]
                {
                    "ALTER TABLE Presets DROP COLUMN Description;",
                    "ALTER TABLE Presets DROP COLUMN CreatedAt;",
                    "ALTER TABLE Presets DROP COLUMN UpdatedAt;",
                    "ALTER TABLE PresetItems DROP COLUMN PackageName;"
                })
                {
                    await ExecuteAsync(connection, transaction, statement, cancellationToken).ConfigureAwait(false);
                }
            }

            await ValidateColumnsAsync(connection, transaction, CurrentVersion, cancellationToken).ConfigureAwait(false);
            await VerifyDatabaseAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(connection, transaction, "PRAGMA user_version=1;", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            return backupPath;
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Workspace schema migration failed; the transaction will roll back. Previous database backup: {backupPath ?? "not required for an empty database"}.", exception);
        }
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode) => new(new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = mode,
        Cache = SqliteCacheMode.Private,
        Pooling = false,
        ForeignKeys = true
    }.ToString());

    private static async Task ValidateColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, int version, CancellationToken cancellationToken)
    {
        var expectedColumns = new Dictionary<string, string[]>
        {
            ["Presets"] = version == 0 ? ["Id", "Name", "Description", "CreatedAt", "UpdatedAt"] : ["Id", "Name"],
            ["PresetItems"] = version == 0 ? ["Id", "PresetId", "PackageId", "PackageName", "Source"] : ["Id", "PresetId", "PackageId", "Source"],
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
                var notNull = name == "Description" ? 0L : 1L;
                if (!string.Equals(reader.GetString(2), "TEXT", StringComparison.OrdinalIgnoreCase)
                    || reader.GetInt64(3) != notNull || reader.GetInt64(5) != primaryKey || reader.GetInt64(6) != 0)
                {
                    throw new InvalidOperationException($"Unrecognized workspace column '{table}.{name}'; no schema changes were committed.");
                }
                columns.Add(name);
            }
            if (!columns.SetEquals(expected))
            {
                throw new InvalidOperationException($"Unrecognized workspace table '{table}' for schema version {version}; no schema changes were committed.");
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

    private static async Task<string> CreateBackupAsync(string dbPath, CancellationToken cancellationToken)
    {
        var backupPath = $"{dbPath}.pre-schema-v1-{Guid.NewGuid():N}.bak";
        var pendingPath = backupPath + ".partial";
        try
        {
            using (new FileStream(pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            // A separate reader includes committed WAL data while the writer reservation prevents changes.
            await using (var source = CreateConnection(dbPath, SqliteOpenMode.ReadOnly))
            await using (var backup = CreateConnection(pendingPath, SqliteOpenMode.ReadWrite))
            {
                await source.OpenAsync(cancellationToken).ConfigureAwait(false);
                await backup.OpenAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                source.BackupDatabase(backup);
                await ExecuteAsync(backup, null, "PRAGMA journal_mode=DELETE;", cancellationToken).ConfigureAwait(false);
                await VerifyDatabaseAsync(backup, null, cancellationToken).ConfigureAwait(false);
            }
            using (var file = new FileStream(pendingPath, FileMode.Open, FileAccess.Write, FileShare.None)) file.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(pendingPath, backupPath);
            return backupPath;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            throw new InvalidOperationException($"Cannot create a verified workspace backup. Original schema preserved; inspect '{pendingPath}'.", exception);
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
