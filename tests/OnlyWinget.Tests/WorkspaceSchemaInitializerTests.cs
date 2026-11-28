using Microsoft.Data.Sqlite;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Storage.Sqlite;

namespace OnlyWinget.Tests;

public sealed class WorkspaceSchemaInitializerTests
{
    [Fact]
    public async Task FreshDatabaseIsCreatedAtCurrentVersionWithoutBackup()
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

    [Fact]
    public async Task PreviousUnversionedSchemaIsRejectedWithoutConversionOrBackup()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreatePreviousFormatAsync();
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains("Unsupported workspace schema version 0", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal("retired description", await fixture.ScalarAsync("SELECT Description FROM Presets;"));
        Assert.Equal("Custom label", await fixture.ScalarAsync("SELECT PackageName FROM PresetItems WHERE Id='item-b';"));
        Assert.Equal("unrelated", await fixture.ScalarAsync("SELECT Value FROM UserNotes;"));
        Assert.Empty(fixture.Backups);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task UnsupportedVersionBlocksLoadAndSaveWithoutChanges(int version)
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreatePreviousFormatAsync();
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
    public async Task ForeignDatabaseIsPreservedWithoutCreatingWorkspaceTables(string sql, string objectName)
    {
        using var fixture = new DatabaseFixture();
        await fixture.OpenAsync();
        await fixture.ExecuteAsync(sql);
        var store = fixture.CreateStore();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        Assert.Contains("Unsupported workspace schema version 0", error.Message);
        Assert.Equal(1L, await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';"));
        Assert.Equal("unrelated", await fixture.ScalarAsync($"SELECT Value FROM {objectName};"));
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
    public async Task CurrentVersionWithMissingUniqueConstraintIsRejected()
    {
        using var fixture = new DatabaseFixture();
        await fixture.CreateStore().SaveAsync(new WorkspaceState([new Preset("Original", [])], "Original"), CancellationToken.None);
        await fixture.OpenAsync();
        await fixture.ExecuteAsync("DROP INDEX IX_Presets_Name;");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateStore().LoadAsync(CancellationToken.None));
        Assert.Contains("Unrecognized workspace uniqueness", error.Message);
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
    public async Task CancelledInitializationDoesNotCreateWorkspaceTables()
    {
        using var fixture = new DatabaseFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WorkspaceSchemaInitializer.InitializeAsync(fixture.Path, cancellation.Token));
        await fixture.OpenAsync();
        Assert.Equal(0L, await fixture.ScalarAsync("PRAGMA user_version;"));
        Assert.Equal(0L, await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_schema WHERE name NOT GLOB 'sqlite_*';"));
    }

    [Fact]
    public async Task ConcurrentStoreInitializationCreatesOneCurrentSchema()
    {
        using var fixture = new DatabaseFixture();
        var states = await Task.WhenAll(
            Task.Run(() => fixture.CreateStore().LoadAsync(CancellationToken.None)),
            Task.Run(() => fixture.CreateStore().LoadAsync(CancellationToken.None)));

        Assert.All(states, state => Assert.Empty(state.Presets));
        await fixture.OpenAsync();
        Assert.Equal(1L, await fixture.ScalarAsync("PRAGMA user_version;"));
    }

    private sealed class DatabaseFixture : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"onlywinget-schema-{Guid.NewGuid():N}");
        public string Path { get; }
        public SqliteConnection Connection { get; }
        public string[] Backups => Directory.GetFiles(directory, "*.bak*");

        public DatabaseFixture(string fileName = "onlywinget.db")
        {
            Directory.CreateDirectory(directory);
            Path = System.IO.Path.Combine(directory, fileName);
            Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false, ForeignKeys = true }.ToString());
        }

        public SqliteWorkspaceStore CreateStore() => new(Path);
        public Task OpenAsync() => Connection.OpenAsync();

        public async Task CreatePreviousFormatAsync()
        {
            await OpenAsync();
            await ExecuteAsync("""
                PRAGMA journal_mode=WAL;
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
