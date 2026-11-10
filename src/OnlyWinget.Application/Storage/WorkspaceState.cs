using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.Storage;

public sealed record WorkspaceState(IReadOnlyList<Preset> Presets, string? ActivePresetName)
{
    public const int CurrentSchemaVersion = 1;

    public static WorkspaceState Empty { get; } = new([], null);
}
