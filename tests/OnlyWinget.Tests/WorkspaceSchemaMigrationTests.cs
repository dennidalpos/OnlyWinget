using Microsoft.Data.Sqlite;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Storage.Sqlite;

namespace OnlyWinget.Tests;

public sealed class WorkspaceSchemaMigrationTests
{
    [Fact]
    public async Task FreshDatabaseUsesReducedVersionedSchemaWithoutBackup()
    {
        using var fixture = new DatabaseFixture();
        var store = fixture.CreateStore();
        var state = new WorkspaceState([new Preset("Fresh", [new PackageIdentity("Git.Git", "winget")])], "Fresh");
        await store.SaveAsync(state, CancellationToken.None);
        await fixture.OpenAsync();

        Assert.Equal(1L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal(["Id", "Name"], await fixture.ColumnsAsync("Presets"));
        Assert.Equal(["Id", "PackageId", "PresetId", "Source"], await fixture.ColumnsAsync("PresetItems"));
        Assert.Empty(fixture.Backups);
        Assert.Equal("Fresh", (await fixture.CreateStore().LoadAsync(CancellationToken.None)).ActivePresetName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyMigrationPreservesIdentitiesMetadataConstraintsAndRestorableBackup(bool useWal)
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        if (useWal) Assert.True(File.Exists(fixture.Path + "-wal"));
        else await fixture.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE;");
        var store = fixture.CreateStore();
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal("Original", loaded.ActivePresetName);
        Assert.Equal([new PackageIdentity("Dummy.App", "winget"), new PackageIdentity("Dummy.App", "custom")], Assert.Single(loaded.Presets).Packages);
        Assert.Equal(1L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("preset-id", await fixture.ScalarAsync("SELECT Id FROM Presets;"));
        Assert.Equal("item-a,item-b", await fixture.ScalarAsync("SELECT group_concat(Id, ',') FROM (SELECT Id FROM PresetItems ORDER BY Id);"));
        Assert.Equal("retained", await fixture.ScalarAsync("SELECT Value FROM WorkspaceMetadata WHERE Key='UnrelatedMetadata';"));
        Assert.Equal("unrelated", await fixture.ScalarAsync("SELECT Value FROM UserNotes;"));
        Assert.Null(await fixture.ScalarAsync("PRAGMA foreign_key_check;"));
        Assert.Equal("ok", await fixture.ScalarAsync("PRAGMA integrity_check;"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("INSERT INTO Presets VALUES ('duplicate', 'Original');"));
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync("INSERT INTO PresetItems VALUES ('orphan', 'missing', 'Dummy.App', 'winget');"));

        var backupPath = Assert.Single(fixture.Backups);
        await using var backup = fixture.OpenBackup(backupPath);
        await backup.OpenAsync();
        await using var command = backup.CreateCommand();
        command.CommandText = "SELECT Description || '|' || CreatedAt || '|' || UpdatedAt FROM Presets;";
        Assert.Equal("retired description|2020-01-01|2020-02-01", await command.ExecuteScalarAsync());
        command.CommandText = "SELECT PackageName FROM PresetItems WHERE Id='item-b';";
        Assert.Equal("Custom label", await command.ExecuteScalarAsync());
        command.CommandText = "SELECT COUNT(*) FROM PresetItems;";
        Assert.Equal(2L, await command.ExecuteScalarAsync());
        command.CommandText = "PRAGMA user_version;";
        Assert.Equal(0L, await command.ExecuteScalarAsync());

        await store.SaveAsync(loaded, CancellationToken.None);
        Assert.Equal("Original", (await fixture.CreateStore().LoadAsync(CancellationToken.None)).ActivePresetName);
        Assert.Single(fixture.Backups);

        // Restore through SQLite rather than copying a live database or ignoring its WAL.
        backup.BackupDatabase(fixture.Connection);
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Equal("Custom label", await fixture.ScalarAsync("SELECT PackageName FROM PresetItems WHERE Id='item-b';"));
        Assert.Equal("ok", await fixture.ScalarAsync("PRAGMA integrity_check;"));
    }

    [Fact]
    public async Task FailedLateColumnDropRollsBackEarlierDropsAndCanRetryAfterRepair()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        await fixture.ExecuteAsync("CREATE INDEX IX_LegacyName ON PresetItems(PackageName);");
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains("Previous database backup:", error.Message);
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal(["CreatedAt", "Description", "Id", "Name", "UpdatedAt"], await fixture.ColumnsAsync("Presets"));
        Assert.Equal("Custom label", await fixture.ScalarAsync("SELECT PackageName FROM PresetItems WHERE Id='item-b';"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        var firstBackup = Assert.Single(fixture.Backups);
        var originalBackup = await File.ReadAllBytesAsync(firstBackup);

        await fixture.ExecuteAsync("DROP INDEX IX_LegacyName;");
        Assert.Equal("Original", (await store.LoadAsync(CancellationToken.None)).ActivePresetName);
        Assert.Equal(1L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal(2, fixture.Backups.Length);
        Assert.Equal(originalBackup, await File.ReadAllBytesAsync(firstBackup));
    }

    [Fact]
    public async Task BackupIoFailureLeavesOriginalSchemaAndDataUntouched()
    {
        using var fixture = new DatabaseFixture(new string('d', 220) + ".db");
        await fixture.CreateLegacyAsync();
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains("Cannot create a verified workspace backup", error.Message);
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Empty(fixture.Backups);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task UnsupportedVersionBlocksLoadAndSaveWithoutBackup(int version)
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        await fixture.ExecuteAsync($"PRAGMA user_version={version};");
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains($"Unsupported workspace schema version {version}", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        Assert.Equal((long)version, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Empty(fixture.Backups);
    }

    [Theory]
    [InlineData("CREATE TABLE UserNotes(Value TEXT); INSERT INTO UserNotes VALUES ('unrelated');", "UserNotes")]
    [InlineData("CREATE TABLE sqliteNotes(Value TEXT); INSERT INTO sqliteNotes VALUES ('unrelated');", "sqliteNotes")]
    [InlineData("CREATE VIEW UserNotes AS SELECT 'unrelated' AS Value;", "UserNotes")]
    public async Task UnrecognizedLegacySchemaIsPreservedWithoutCreatingWorkspaceTables(string sql, string objectName)
    {
        using var fixture = new DatabaseFixture();
        await fixture.OpenAsync();
        await fixture.ExecuteAsync(sql);
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains("Unrecognized workspace table", error.Message);
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';"));
        Assert.Equal("unrelated", await fixture.ScalarAsync($"SELECT Value FROM {objectName};"));
        Assert.Empty(fixture.Backups);
    }

    [Fact]
    public async Task MissingUniqueConstraintBlocksLegacyMigration()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        await fixture.ExecuteAsync("DROP INDEX IX_Presets_Name;");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateStore().LoadAsync(CancellationToken.None));
        Assert.Contains("Unrecognized workspace uniqueness", error.Message);
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Empty(fixture.Backups);
    }

    [Fact]
    public async Task CurrentVersionWithUnexpectedColumnIsRejectedWithoutSaving()
    {
        using var fixture = new DatabaseFixture();
        var store = fixture.CreateStore();
        await store.SaveAsync(new WorkspaceState([new Preset("Original", [])], "Original"), CancellationToken.None);
        await fixture.OpenAsync();
        await fixture.ExecuteAsync("ALTER TABLE Presets ADD COLUMN Unexpected TEXT;");
        var reopenedStore = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => reopenedStore.LoadAsync(CancellationToken.None));
        Assert.Contains("Unrecognized workspace column", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopenedStore.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        Assert.Equal("Original", await fixture.ScalarAsync("SELECT Name FROM Presets;"));
        Assert.Empty(fixture.Backups);
    }

    [Fact]
    public async Task DatabasePathWithConnectionStringPunctuationRoundTrips()
    {
        using var fixture = new DatabaseFixture("workspace;mode=readonly.db");
        var store = fixture.CreateStore();
        await store.SaveAsync(new WorkspaceState([new Preset("Original", [])], "Original"), CancellationToken.None);
        Assert.Equal("Original", (await fixture.CreateStore().LoadAsync(CancellationToken.None)).ActivePresetName);
    }

    [Fact]
    public async Task InvalidForeignKeyStopsMigrationBeforeBackupOrColumnRemoval()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        await fixture.ExecuteAsync("PRAGMA foreign_keys=OFF; INSERT INTO PresetItems VALUES ('orphan', 'missing', 'Dummy.App', 'Orphan', 'winget');");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateStore().LoadAsync(CancellationToken.None));
        Assert.Contains("invalid foreign keys", error.Message);
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal(3L, await fixture.ScalarAsync("SELECT COUNT(*) FROM PresetItems;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Empty(fixture.Backups);
    }

    [Fact]
    public async Task CancelledMigrationPreservesLegacySchemaWithoutBackup()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkspaceSchemaMigration.InitializeAsync(fixture.Path, cancellation.Token));
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Empty(fixture.Backups);
    }

    [Fact]
    public async Task ConcurrentStoreInitializationCreatesOneBackup()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateLegacyAsync();
        var states = await Task.WhenAll(
            Task.Run(() => fixture.CreateStore().LoadAsync(CancellationToken.None)),
            Task.Run(() => fixture.CreateStore().LoadAsync(CancellationToken.None)));

        Assert.All(states, state => Assert.Equal("Original", state.ActivePresetName));
        Assert.Single(fixture.Backups);
        Assert.Equal(1L, await fixture.ScalarAsync("PRAGMA user_version;"));
    }

    private sealed class DatabaseFixture : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"onlywinget-schema-{Guid.NewGuid():N}");
        public string Path { get; }
        public SqliteConnection Connection { get; }
        public string[] Backups => Directory.GetFiles(directory, "*.bak");

        public DatabaseFixture(string fileName = "onlywinget.db")
        {
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, fileName);
            Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false, ForeignKeys = true }.ToString());
        }

        public SqliteWorkspaceStore CreateStore() => new(Path, System.IO.Path.Combine(directory, "absent.json"));
        public Task OpenAsync() => Connection.OpenAsync();
        public SqliteConnection OpenBackup(string path) => new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());

        public async Task CreateLegacyAsync()
        {
            await OpenAsync();
            await ExecuteAsync("""
                PRAGMA journal_mode=WAL;
                PRAGMA wal_autocheckpoint=0;
                CREATE TABLE Presets(Id TEXT NOT NULL PRIMARY KEY, Name TEXT NOT NULL, Description TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
                CREATE UNIQUE INDEX IX_Presets_Name ON Presets(Name);
                CREATE TABLE PresetItems(Id TEXT NOT NULL PRIMARY KEY, PresetId TEXT NOT NULL, PackageId TEXT NOT NULL, PackageName TEXT NOT NULL, Source TEXT NOT NULL,
                    FOREIGN KEY(PresetId) REFERENCES Presets(Id) ON DELETE CASCADE);
                CREATE INDEX IX_PresetItems_PresetId ON PresetItems(PresetId);
                CREATE TABLE WorkspaceMetadata(Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL);
                CREATE TABLE UserNotes(Value TEXT);
                INSERT INTO Presets VALUES ('preset-id', 'Original', 'retired description', '2020-01-01', '2020-02-01');
                INSERT INTO PresetItems VALUES ('item-a', 'preset-id', 'Dummy.App', 'Winget label', 'winget');
                INSERT INTO PresetItems VALUES ('item-b', 'preset-id', 'Dummy.App', 'Custom label', 'custom');
                INSERT INTO WorkspaceMetadata VALUES ('ActivePresetName', 'Original'), ('UnrelatedMetadata', 'retained');
                INSERT INTO UserNotes VALUES ('unrelated');
                """);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ScalarAsync(string sql)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync();
        }

        public async Task<string[]> ColumnsAsync(string table)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{table}');";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new List<string>();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
            return columns.Order(StringComparer.Ordinal).ToArray();
        }

        public void Dispose()
        {
            Connection.Dispose();
            using var pooledConnection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path }.ToString());
            SqliteConnection.ClearPool(pooledConnection);
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
