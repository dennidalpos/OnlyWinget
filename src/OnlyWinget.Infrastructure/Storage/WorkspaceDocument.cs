using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Infrastructure.Storage;

internal sealed record WorkspaceDocument(
    int SchemaVersion,
    IReadOnlyList<Preset> Presets,
    string? ActivePresetName);
