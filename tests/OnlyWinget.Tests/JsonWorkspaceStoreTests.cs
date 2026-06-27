using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Storage;

namespace OnlyWinget.Tests;

public sealed class JsonWorkspaceStoreTests
{
    [Fact]
    public async Task SaveAndLoadUseWorkspaceV1Schema()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}", "workspace-v1.json");
        var store = new JsonWorkspaceStore(filePath);
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
    public async Task LoadMalformedWorkspaceReturnsEmptyState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "workspace-v1.json");
        await File.WriteAllTextAsync(filePath, "{not-json");

        var loaded = await new JsonWorkspaceStore(filePath).LoadAsync(CancellationToken.None);

        Assert.Empty(loaded.Presets);
    }

    [Fact]
    public async Task CancelledSavePreservesExistingWorkspaceAndRemovesTemporaryFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}", "workspace-v1.json");
        var store = new JsonWorkspaceStore(filePath);
        var original = new WorkspaceState([new Preset("Original", [])], "Original");
        await store.SaveAsync(original, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(new WorkspaceState([new Preset("Replacement", [])], "Replacement"), cancellation.Token));

        Assert.Equal("Original", (await store.LoadAsync(CancellationToken.None)).ActivePresetName);
        Assert.False(File.Exists(filePath + ".tmp"));
    }

    [Fact]
    public async Task FailedReplaceRemovesTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"onlywinget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var store = new JsonWorkspaceStore(directory);

        await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
            store.SaveAsync(new WorkspaceState([new Preset("Blocked", [])], "Blocked"), CancellationToken.None));

        Assert.False(File.Exists(directory + ".tmp"));
    }
}
