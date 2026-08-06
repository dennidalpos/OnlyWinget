using System.Text.Json;
using System.Text.Json.Serialization;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Services;

[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}

internal sealed class JsonAppSettingsService : IAppSettingsService
{
    private readonly string filePath;
    private readonly SemaphoreSlim saveGate = new(1, 1);

    public JsonAppSettingsService(string filePath)
    {
        this.filePath = filePath;
        Current = Load(filePath);
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        StorageConstants.ApplicationFolderName,
        "settings.json");

    public event EventHandler? Changed;

    public AppSettings Current { get; private set; }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = filePath + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings),
                cancellationToken);
            File.Move(temporaryPath, filePath, true);
            Current = settings;
        }
        finally
        {
            saveGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task ResetAsync(CancellationToken cancellationToken) => SaveAsync(new AppSettings(), cancellationToken);

    private static AppSettings Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }
}
