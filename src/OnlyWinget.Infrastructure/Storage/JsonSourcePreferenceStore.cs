using System.Text.Json;
using System.Threading;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Infrastructure.Storage;

public sealed class JsonSourcePreferenceStore(string filePath) : ISourcePreferenceStore
{
    private readonly SemaphoreSlim saveGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OnlyWinget",
        "source-preferences-v1.json");

    public async Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken)
    {
        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return SourcePreferences.Empty;
            }

            try
            {
                await using var stream = File.OpenRead(filePath);
                var document = await JsonSerializer.DeserializeAsync<SourcePreferencesDocument>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                return document is { SchemaVersion: 1 }
                    ? Normalize(new SourcePreferences(document.DisabledSources ?? []))
                    : SourcePreferences.Empty;
            }
            catch (JsonException)
            {
                return SourcePreferences.Empty;
            }
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            try
            {
                await using (var stream = File.Create(temporaryPath))
                {
                    var normalized = Normalize(preferences);
                    await JsonSerializer.SerializeAsync(
                            stream,
                            new SourcePreferencesDocument(1, normalized.DisabledSources),
                            JsonOptions,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            saveGate.Release();
        }
    }

    private static SourcePreferences Normalize(SourcePreferences preferences) =>
        new(preferences.DisabledSources
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private sealed record SourcePreferencesDocument(int SchemaVersion, IReadOnlyList<string>? DisabledSources);
}
