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
}
