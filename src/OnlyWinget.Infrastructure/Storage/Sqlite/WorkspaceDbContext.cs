using Microsoft.EntityFrameworkCore;

namespace OnlyWinget.Infrastructure.Storage.Sqlite;

public sealed class WorkspaceDbContext : DbContext
{
    private readonly string dbPath;

    public DbSet<PresetEntity> Presets => Set<PresetEntity>();

    public DbSet<PresetItemEntity> PresetItems => Set<PresetItemEntity>();

    public DbSet<WorkspaceMetadataEntity> WorkspaceMetadata => Set<WorkspaceMetadataEntity>();

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core model is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "EF Core model is defined statically.")]
    public WorkspaceDbContext(string dbPath)
    {
        this.dbPath = dbPath;
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core model is defined statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "EF Core model is defined statically.")]
    public WorkspaceDbContext(DbContextOptions<WorkspaceDbContext> options)
        : base(options)
    {
        dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(dbPath))
        {
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PresetEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired();
            entity.HasMany(e => e.Items)
                .WithOne(e => e.Preset)
                .HasForeignKey(e => e.PresetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PresetItemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PackageId).IsRequired();
        });

        modelBuilder.Entity<WorkspaceMetadataEntity>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Value).IsRequired();
        });
    }

    public async Task InitializeWalModeAsync(CancellationToken cancellationToken = default)
    {
        await Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", cancellationToken).ConfigureAwait(false);
        await Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;", cancellationToken).ConfigureAwait(false);
        await Database.ExecuteSqlRawAsync("PRAGMA mmap_size=268435456;", cancellationToken).ConfigureAwait(false);
        await Database.ExecuteSqlRawAsync("PRAGMA cache_size=-64000;", cancellationToken).ConfigureAwait(false);
    }
}
