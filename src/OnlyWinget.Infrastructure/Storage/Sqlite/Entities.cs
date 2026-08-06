namespace OnlyWinget.Infrastructure.Storage.Sqlite;

public sealed class PresetEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<PresetItemEntity> Items { get; set; } = [];
}

public sealed class PresetItemEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string PresetId { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public PresetEntity? Preset { get; set; }
}

public sealed class WorkspaceMetadataEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class OperationLogEntity
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string OperationType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int? ExitCode { get; set; }

    public string? DetailsText { get; set; }
}
