using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record UpdatesPresentationState(
    IReadOnlyList<UpdateRow> Updates,
    SelectionHeaderState HeaderState,
    IReadOnlyList<PresentationCommand> Commands,
    bool IsLoading,
    string? Error);
