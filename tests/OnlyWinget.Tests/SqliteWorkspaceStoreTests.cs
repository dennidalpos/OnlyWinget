using Microsoft.Data.Sqlite;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.Storage.Sqlite;

namespace OnlyWinget.Tests;

public sealed class SqliteWorkspaceStoreTests
{
    [Fact]
    public async Task FailedLoadBlocksSaveAndPreservesOriginalPresetUntilSuccessfulReload()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");
        var store = new SqliteWorkspaceStore(dbPath, Path.Combine(tempFolder, "legacy.json"));
        var original = new WorkspaceState([new Preset("Original", [new PackageIdentity("Dummy.App")])], "Original");
        await store.SaveAsync(original, CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PresetItems SET PackageId = '';";
        await command.ExecuteNonQueryAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        command.CommandText = "SELECT Name FROM Presets;";
        Assert.Equal("Original", await command.ExecuteScalarAsync());

        command.CommandText = "UPDATE PresetItems SET PackageId = 'Dummy.App';";
        await command.ExecuteNonQueryAsync();
        Assert.Equal("Original", (await store.LoadAsync(CancellationToken.None)).ActivePresetName);
        await store.SaveAsync(original, CancellationToken.None);
        Assert.Equal("Original", (await store.LoadAsync(CancellationToken.None)).ActivePresetName);
    }

    [Fact]
    public async Task MalformedLegacyWorkspaceDoesNotBecomeAnEmptyWritableWorkspace()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);
        var legacyPath = Path.Combine(tempFolder, "workspace-v1.json");
        const string damagedJson = "{ damaged";
        await File.WriteAllTextAsync(legacyPath, damagedJson);
        var store = new SqliteWorkspaceStore(Path.Combine(tempFolder, "onlywinget.db"), legacyPath);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(WorkspaceState.Empty, CancellationToken.None));
        Assert.Equal(damagedJson, await File.ReadAllTextAsync(legacyPath));
    }

    [Fact]
    public async Task SaveAndLoadUseSqliteDatabase()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");
        var store = new SqliteWorkspaceStore(dbPath);
        var state = new WorkspaceState(
            [new Preset("Default", [new PackageIdentity("Git.Git")])],
            "Default");

        await store.SaveAsync(state, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(state.ActivePresetName, loaded.ActivePresetName);
        var loadedPreset = Assert.Single(loaded.Presets);
        Assert.Equal("Default", loadedPreset.Name);
        Assert.Equal([new PackageIdentity("Git.Git")], loadedPreset.Packages);
    }

    [Fact]
    public async Task TransparentMigrationFromLegacyJson()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var jsonPath = Path.Combine(tempFolder, "workspace-v1.json");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");

        var legacyStore = new JsonWorkspaceStore(jsonPath);
        var legacyState = new WorkspaceState(
            [new Preset("MigratedPreset", [new PackageIdentity("Microsoft.PowerToys")])],
            "MigratedPreset");
        await legacyStore.SaveAsync(legacyState, CancellationToken.None);

        var sqliteStore = new SqliteWorkspaceStore(dbPath, jsonPath);
        var loaded = await sqliteStore.LoadAsync(CancellationToken.None);

        Assert.Equal("MigratedPreset", loaded.ActivePresetName);
        var loadedPreset = Assert.Single(loaded.Presets);
        Assert.Equal("MigratedPreset", loadedPreset.Name);
        Assert.Equal([new PackageIdentity("Microsoft.PowerToys")], loadedPreset.Packages);
    }

    [Fact]
    public async Task WalModeIsConfiguredCorrectly()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");
        var store = new SqliteWorkspaceStore(dbPath);

        await store.SaveAsync(WorkspaceState.Empty, CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        var mode = await command.ExecuteScalarAsync();

        Assert.Equal("wal", mode?.ToString()?.ToLowerInvariant());
    }

    [Fact]
    public async Task CancelledSaveThrowsAndDoesNotCorruptDatabase()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");
        var store = new SqliteWorkspaceStore(dbPath);

        var original = new WorkspaceState([new Preset("Original", [])], "Original");
        await store.SaveAsync(original, CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(new WorkspaceState([new Preset("Replacement", [])], "Replacement"), cancellation.Token));

        var loaded = await store.LoadAsync(CancellationToken.None);
        Assert.Equal("Original", loaded.ActivePresetName);
    }
}
