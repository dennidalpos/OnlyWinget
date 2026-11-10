using OnlyWinget.Application.Storage;
using OnlyWinget.Infrastructure.Storage;

namespace OnlyWinget.Tests;

public sealed class JsonSourcePreferenceStoreTests
{
    [Theory]
    [InlineData("{not-json")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"disabledSources\":[\"winget\"],\"defaultSourcesConfigured\":true}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"disabledSources\":[null]}")]
    public async Task UnreadablePreferencesBlockLoadAndSaveAcrossRestart(string original)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"onlywinget-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            await File.WriteAllTextAsync(path, original);
            var store = new JsonSourcePreferenceStore(path);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(CancellationToken.None));
            Assert.Contains(path, failure.Message);
            Assert.Contains("preserved", failure.Message);
            foreach (var instance in new[] { store, new JsonSourcePreferenceStore(path) })
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => instance.SaveAsync(SourcePreferences.Empty, CancellationToken.None));
                Assert.Equal(original, await File.ReadAllTextAsync(path));
                Assert.False(File.Exists(path + ".tmp"));
            }
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":1,\"disabledSources\":[\"winget\"],\"defaultSourcesConfigured\":true}");
            var repaired = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(new[] { "winget" }, repaired.DisabledSources);
            Assert.True(repaired.DefaultSourcesConfigured);
            await store.SaveAsync(repaired, CancellationToken.None);
            Assert.True((await new JsonSourcePreferenceStore(path).LoadAsync(CancellationToken.None)).DefaultSourcesConfigured);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task MissingPreferencesAllowFirstSaveAndCancelledSavePreservesChoices()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"onlywinget-source-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            var store = new JsonSourcePreferenceStore(path);
            Assert.Empty((await store.LoadAsync(CancellationToken.None)).DisabledSources);
            await store.SaveAsync(new SourcePreferences(["winget"], true), CancellationToken.None);
            var original = await File.ReadAllTextAsync(path);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(SourcePreferences.Empty, cancelled.Token));
            Assert.Equal(original, await File.ReadAllTextAsync(path));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
