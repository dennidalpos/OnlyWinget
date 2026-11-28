using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record UpdatesPresentationState(
    IReadOnlyList<UpdateRow> Updates,
    SelectionHeaderState HeaderState,
    IReadOnlyList<OperationResultRow> OperationResults,
    IReadOnlyList<UiCommand> Commands,
    bool IsLoading,
    bool IsExecuting,
    string? Error);
