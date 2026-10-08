using System.Text.Json;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Infrastructure.Storage;

public sealed class JsonSourcePreferenceStore(
    string filePath,
    Action<string, Exception>? logger = null,
    ILogger<JsonSourcePreferenceStore>? storeLogger = null) : ISourcePreferenceStore
{
    private readonly SemaphoreSlim saveGate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        StorageConstants.ApplicationFolderName,
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

            return await ReadExistingAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            saveGate.Release();
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "SourcePreferencesDocument DTO is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "SourcePreferencesDocument DTO is defined statically.")]
    public async Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Reject unreadable existing preferences even when this instance never loaded them.
            if (File.Exists(filePath)) await ReadExistingAsync(cancellationToken).ConfigureAwait(false);
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
                            new SourcePreferencesDocument(1, normalized.DisabledSources, normalized.DefaultSourcesConfigured),
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
            .ToArray(),
            preferences.DefaultSourcesConfigured);

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "SourcePreferencesDocument DTO is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "SourcePreferencesDocument DTO is defined statically.")]
    private async Task<SourcePreferences> ReadExistingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await JsonSerializer.DeserializeAsync<SourcePreferencesDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (document is not { SchemaVersion: 1, DisabledSources: not null } ||
                document.DisabledSources.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException("Unsupported source preferences schema or invalid disabled-source list.");
            }
            return Normalize(new SourcePreferences(document.DisabledSources, document.DefaultSourcesConfigured));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger?.Invoke("JsonSourcePreferenceStore.ReadExistingAsync", exception);
            storeLogger?.LogError(exception, "Cannot read source preferences at '{FilePath}'", filePath);
            throw new InvalidDataException($"Source preferences '{filePath}' are unreadable. Restore or repair the file before saving; it has been preserved.", exception);
        }
    }

    private sealed record SourcePreferencesDocument(int SchemaVersion, IReadOnlyList<string>? DisabledSources, bool DefaultSourcesConfigured = false);
}
