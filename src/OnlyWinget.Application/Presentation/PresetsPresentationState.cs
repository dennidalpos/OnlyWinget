using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record PresetsPresentationState(
    IReadOnlyList<string> PresetNames,
    string? ActivePresetName,
    IReadOnlyList<PresetPackageRow> Packages,
    SelectionHeaderState HeaderState,
    IReadOnlyList<OperationResultRow> OperationResults,
    IReadOnlyList<PresentationCommand> Commands,
    bool IsExecuting,
    string? Error);
