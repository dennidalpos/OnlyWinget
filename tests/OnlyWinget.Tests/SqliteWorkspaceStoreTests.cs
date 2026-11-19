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
    public async Task ConcurrentWorkspaceChangeRequiresReloadBeforeSaving()
    {
        var path = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}", "onlywinget.db");
        var first = new SqliteWorkspaceStore(path);
        var second = new SqliteWorkspaceStore(path);
        var original = new WorkspaceState([new Preset("Original", [])], "Original");
        await first.SaveAsync(original, CancellationToken.None);
        var previous = await second.LoadAsync(CancellationToken.None);
        var updated = new WorkspaceState([new Preset("Original", [new PackageIdentity("Test.App")])], "Original");
        await first.SaveAsync(updated, CancellationToken.None);

        var error = await Assert.ThrowsAsync<WorkspaceWriteConflictException>(() =>
            second.SaveAsync(previous, CancellationToken.None));
        Assert.Contains("reopen OnlyWinget", error.Message);
        var latest = await second.LoadAsync(CancellationToken.None);
        Assert.Equal("Test.App", Assert.Single(Assert.Single(latest.Presets).Packages).Id);
        await second.SaveAsync(latest with { Presets = [.. latest.Presets, new Preset("Second", [])] },
            CancellationToken.None);
        Assert.Equal(2, (await first.LoadAsync(CancellationToken.None)).Presets.Count);
    }

    [Fact]
    public async Task FailedLoadBlocksSaveAndPreservesOriginalPresetUntilSuccessfulReload()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempFolder, "onlywinget.db");
        var store = new SqliteWorkspaceStore(dbPath);
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
