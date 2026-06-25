using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record PresetsPresentationState(
    IReadOnlyList<string> PresetNames,
    string? ActivePresetName,
    IReadOnlyList<PresetPackageRow> Packages,
    SelectionHeaderState HeaderState,
    IReadOnlyList<PresentationCommand> Commands,
    string? Error);
