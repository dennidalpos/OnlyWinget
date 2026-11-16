using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Infrastructure.Storage.Sqlite;

public sealed class SqliteWorkspaceStore : IWorkspaceStore
{
    private readonly string dbPath;
    private readonly Action<string, Exception>? logger;
    private readonly ILogger<SqliteWorkspaceStore>? storeLogger;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool isInitialized;
    private bool loadFailed;

    private static readonly Func<WorkspaceDbContext, string, Task<WorkspaceMetadataEntity?>> CompiledGetMetadataByKeyQuery =
        EF.CompileAsyncQuery((WorkspaceDbContext ctx, string key) =>
            ctx.WorkspaceMetadata.AsNoTracking().FirstOrDefault(m => m.Key == key));

    public SqliteWorkspaceStore(
        string dbPath,
        Action<string, Exception>? logger = null,
        ILogger<SqliteWorkspaceStore>? storeLogger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        this.dbPath = dbPath;
        this.logger = logger;
        this.storeLogger = storeLogger;
    }

    public static string DefaultFilePath => Path.Combine(AppDataPaths.Root, "onlywinget.db");

    public async Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken)
    {
        await saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            await using var context = new WorkspaceDbContext(dbPath);

            var presetEntities = await context.Presets
                .Include(p => p.Items)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var activePresetMeta = await CompiledGetMetadataByKeyQuery(context, "ActivePresetName").ConfigureAwait(false);

            var presets = presetEntities.Select(p => new Preset(
                p.Name,
                p.Items.Select(item => new PackageIdentity(item.PackageId, item.Source)).ToList()
            )).ToList();

            var activePresetName = string.IsNullOrWhiteSpace(activePresetMeta?.Value)
                ? null
                : activePresetMeta.Value;

            loadFailed = false;
            return new WorkspaceState(presets, activePresetName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loadFailed = true;
            logger?.Invoke("SqliteWorkspaceStore.LoadAsync", exception);
            storeLogger?.LogError(exception, "Failed to load workspace state from SQLite database at '{DbPath}'", dbPath);
            throw new InvalidOperationException($"Unable to load the workspace. Existing data has been preserved; reload successfully before saving. {exception.Message}", exception);
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
            if (loadFailed)
            {
                throw new InvalidOperationException("Saving is disabled after a workspace load failure. Reload the workspace successfully before saving.");
            }
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            await using var context = new WorkspaceDbContext(dbPath);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var existingItems = await context.PresetItems.ToListAsync(cancellationToken).ConfigureAwait(false);
            context.PresetItems.RemoveRange(existingItems);

            var existingPresets = await context.Presets.ToListAsync(cancellationToken).ConfigureAwait(false);
            context.Presets.RemoveRange(existingPresets);

            foreach (var preset in state.Presets)
            {
                var presetEntity = new PresetEntity
                {
                    Name = preset.Name
                };

                foreach (var package in preset.Packages)
                {
                    presetEntity.Items.Add(new PresetItemEntity
                    {
                        PresetId = presetEntity.Id,
                        PackageId = package.Id,
                        Source = package.Source ?? string.Empty
                    });
                }

                context.Presets.Add(presetEntity);
            }

            var activeMeta = await context.WorkspaceMetadata
                .FirstOrDefaultAsync(m => m.Key == "ActivePresetName", cancellationToken)
                .ConfigureAwait(false);

            if (activeMeta is null)
            {
                context.WorkspaceMetadata.Add(new WorkspaceMetadataEntity
                {
                    Key = "ActivePresetName",
                    Value = state.ActivePresetName ?? string.Empty
                });
            }
            else
            {
                activeMeta.Value = state.ActivePresetName ?? string.Empty;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            saveGate.Release();
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core model is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "EF Core model is defined statically.")]
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await WorkspaceSchemaInitializer.InitializeAsync(dbPath, cancellationToken).ConfigureAwait(false);

        await using (var context = new WorkspaceDbContext(dbPath))
        {
            await context.InitializeWalModeAsync(cancellationToken).ConfigureAwait(false);
        }

        isInitialized = true;
    }
}
