using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Infrastructure.Storage.Sqlite;

public sealed class SqliteWorkspaceStore : IWorkspaceStore
{
    private readonly string dbPath;
    private readonly string legacyJsonPath;
    private readonly Action<string, Exception>? logger;
    private readonly ILogger<SqliteWorkspaceStore>? storeLogger;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private bool isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly Func<WorkspaceDbContext, string, Task<WorkspaceMetadataEntity?>> CompiledGetMetadataByKeyQuery =
        EF.CompileAsyncQuery((WorkspaceDbContext ctx, string key) =>
            ctx.WorkspaceMetadata.AsNoTracking().FirstOrDefault(m => m.Key == key));

    public SqliteWorkspaceStore(
        string dbPath,
        string? legacyJsonPath = null,
        Action<string, Exception>? logger = null,
        ILogger<SqliteWorkspaceStore>? storeLogger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        this.dbPath = dbPath;
        this.legacyJsonPath = legacyJsonPath ?? JsonWorkspaceStore.DefaultFilePath;
        this.logger = logger;
        this.storeLogger = storeLogger;
    }

    public static string DefaultFilePath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, StorageConstants.ApplicationFolderName, "onlywinget.db");
        }
    }

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

            return new WorkspaceState(presets, activePresetName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.Invoke("SqliteWorkspaceStore.LoadAsync", exception);
            storeLogger?.LogError(exception, "Failed to load workspace state from SQLite database at '{DbPath}'", dbPath);
            return WorkspaceState.Empty;
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
            await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            await using var context = new WorkspaceDbContext(dbPath);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var existingItems = await context.PresetItems.ToListAsync(cancellationToken).ConfigureAwait(false);
            context.PresetItems.RemoveRange(existingItems);

            var existingPresets = await context.Presets.ToListAsync(cancellationToken).ConfigureAwait(false);
            context.Presets.RemoveRange(existingPresets);

            var now = DateTimeOffset.UtcNow;
            foreach (var preset in state.Presets)
            {
                var presetEntity = new PresetEntity
                {
                    Name = preset.Name,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                foreach (var package in preset.Packages)
                {
                    presetEntity.Items.Add(new PresetItemEntity
                    {
                        PresetId = presetEntity.Id,
                        PackageId = package.Id,
                        PackageName = package.Id,
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

        await using (var context = new WorkspaceDbContext(dbPath))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            await context.InitializeWalModeAsync(cancellationToken).ConfigureAwait(false);

            var hasPresets = await context.Presets.AnyAsync(cancellationToken).ConfigureAwait(false);
            var hasMetadata = await context.WorkspaceMetadata.AnyAsync(cancellationToken).ConfigureAwait(false);

            if (!hasPresets && !hasMetadata && File.Exists(legacyJsonPath))
            {
                await PerformTransparentMigrationAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }

        isInitialized = true;
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Legacy WorkspaceDocument DTO is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Legacy WorkspaceDocument DTO is defined statically.")]
    private async Task PerformTransparentMigrationAsync(WorkspaceDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            WorkspaceDocument? legacyDocument;
            await using (var stream = File.OpenRead(legacyJsonPath))
            {
                legacyDocument = await JsonSerializer.DeserializeAsync<WorkspaceDocument>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (legacyDocument is null || legacyDocument.Presets is null || legacyDocument.Presets.Count == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var preset in legacyDocument.Presets)
            {
                var presetEntity = new PresetEntity
                {
                    Name = preset.Name,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                foreach (var package in preset.Packages)
                {
                    presetEntity.Items.Add(new PresetItemEntity
                    {
                        PresetId = presetEntity.Id,
                        PackageId = package.Id,
                        PackageName = package.Id,
                        Source = package.Source ?? string.Empty
                    });
                }

                context.Presets.Add(presetEntity);
            }

            if (!string.IsNullOrEmpty(legacyDocument.ActivePresetName))
            {
                context.WorkspaceMetadata.Add(new WorkspaceMetadataEntity
                {
                    Key = "ActivePresetName",
                    Value = legacyDocument.ActivePresetName
                });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            storeLogger?.LogInformation("Transparently migrated legacy workspace JSON from '{LegacyPath}' to SQLite database at '{DbPath}'", legacyJsonPath, dbPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.Invoke("SqliteWorkspaceStore.PerformTransparentMigrationAsync", exception);
            storeLogger?.LogWarning(exception, "Failed transparent migration from legacy workspace JSON file '{LegacyPath}'", legacyJsonPath);
        }
    }
}
