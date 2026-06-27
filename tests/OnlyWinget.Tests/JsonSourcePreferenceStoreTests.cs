using OnlyWinget.Application.Storage;
using OnlyWinget.Infrastructure.Storage;

namespace OnlyWinget.Tests;

public sealed class JsonSourcePreferenceStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"onlywinget-source-preferences-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoadRoundTripNormalizesDisabledSources()
    {
        var path = Path.Combine(directory, "source-preferences-v1.json");
        var store = new JsonSourcePreferenceStore(path);

        await store.SaveAsync(new SourcePreferences([" msstore ", "winget", "WINGET"]), CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(["msstore", "winget"], loaded.DisabledSources);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
