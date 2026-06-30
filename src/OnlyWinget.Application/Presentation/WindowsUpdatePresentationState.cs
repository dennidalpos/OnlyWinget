using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record WindowsUpdatePresentationState(
    IReadOnlyList<WindowsUpdateRow> Updates,
    SelectionHeaderState HeaderState,
    IReadOnlyList<WindowsUpdateResultRow> Results,
    IReadOnlyList<UiCommand> Commands,
    bool IsScanning,
    bool IsInstalling,
    string? Error);
