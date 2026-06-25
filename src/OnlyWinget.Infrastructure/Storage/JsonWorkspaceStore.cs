using System.Text.Json;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Infrastructure.Storage;

public sealed class JsonWorkspaceStore(string filePath) : IWorkspaceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultFilePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "OnlyWinget", "workspace-v1.json");
        }
    }

    public async Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return WorkspaceState.Empty;
        }

        await using var stream = File.OpenRead(filePath);
        var document = await JsonSerializer.DeserializeAsync<WorkspaceDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (document is null || document.SchemaVersion != WorkspaceState.CurrentSchemaVersion)
        {
            return WorkspaceState.Empty;
        }

        return new WorkspaceState(document.Presets, document.ActivePresetName);
    }

    public async Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new WorkspaceDocument(
            WorkspaceState.CurrentSchemaVersion,
            state.Presets,
            state.ActivePresetName);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }
}
