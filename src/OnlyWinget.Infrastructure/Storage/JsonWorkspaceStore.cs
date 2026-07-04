using System.Text.Json;
using System.Threading;
using OnlyWinget.Application.Storage;

namespace OnlyWinget.Infrastructure.Storage;

public sealed class JsonWorkspaceStore(string filePath) : IWorkspaceStore
{
    private readonly SemaphoreSlim saveGate = new(1, 1);

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
        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(filePath))
            {
                return WorkspaceState.Empty;
            }

            WorkspaceDocument? document;
            try
            {
                await using var stream = File.OpenRead(filePath);
                document = await JsonSerializer.DeserializeAsync<WorkspaceDocument>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return WorkspaceState.Empty;
            }

            if (document is null || document.SchemaVersion != WorkspaceState.CurrentSchemaVersion)
            {
                return WorkspaceState.Empty;
            }

            return new WorkspaceState(document.Presets, document.ActivePresetName);
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new WorkspaceDocument(
                WorkspaceState.CurrentSchemaVersion,
                state.Presets,
                state.ActivePresetName);

            var temporaryPath = filePath + ".tmp";
            try
            {
                await using (var stream = File.Create(temporaryPath))
                {
                    await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
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
}
