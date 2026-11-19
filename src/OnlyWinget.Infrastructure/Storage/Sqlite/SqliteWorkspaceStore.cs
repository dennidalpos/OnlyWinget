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

    private const string ActivePresetKey = "ActivePresetName";
    private WorkspaceState baseline = WorkspaceState.Empty;

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

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var presetEntities = await context.Presets
                .Include(p => p.Items)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var active = await context.WorkspaceMetadata.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Key == ActivePresetKey, cancellationToken).ConfigureAwait(false);
            var loaded = ReadSnapshot(presetEntities, active);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            baseline = loaded;
            loadFailed = false;
            return loaded;
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

            var existingPresets = await context.Presets.Include(preset => preset.Items)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var activeMeta = await context.WorkspaceMetadata
                .FirstOrDefaultAsync(item => item.Key == ActivePresetKey, cancellationToken).ConfigureAwait(false);
            var current = ReadSnapshot(existingPresets, activeMeta);
            if (baseline.ActivePresetName != current.ActivePresetName ||
                !SnapshotRows(baseline).SequenceEqual(SnapshotRows(current)))
            {
                throw new WorkspaceWriteConflictException();
            }

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

            if (activeMeta is null)
            {
                context.WorkspaceMetadata.Add(new WorkspaceMetadataEntity
                {
                    Key = ActivePresetKey,
                    Value = state.ActivePresetName ?? string.Empty
                });
            }
            else
            {
                activeMeta.Value = state.ActivePresetName ?? string.Empty;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            baseline = new WorkspaceState(state.Presets.ToArray(),
                string.IsNullOrWhiteSpace(state.ActivePresetName) ? null : state.ActivePresetName);
        }
        finally
        {
            saveGate.Release();
        }
    }

    private static WorkspaceState ReadSnapshot(IEnumerable<PresetEntity> presets, WorkspaceMetadataEntity? active) =>
        new(presets.Select(preset => new Preset(preset.Name,
                preset.Items.Select(item => new PackageIdentity(item.PackageId, item.Source)).ToArray())).ToArray(),
            string.IsNullOrWhiteSpace(active?.Value) ? null : active.Value);

    private static IEnumerable<(string Preset, string? Id, string? Source)> SnapshotRows(WorkspaceState state)
    {
        foreach (var preset in state.Presets.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            yield return (preset.Name, null, null);
            foreach (var package in preset.Packages.OrderBy(item => item.Id, StringComparer.Ordinal)
                         .ThenBy(item => item.Source, StringComparer.Ordinal))
            {
                yield return (preset.Name, package.Id, package.Source);
            }
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
