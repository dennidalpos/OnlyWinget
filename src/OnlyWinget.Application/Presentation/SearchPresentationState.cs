using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record SearchPresentationState(
    IReadOnlyList<SearchResultRow> Results,
    SelectionHeaderState HeaderState,
    IReadOnlyList<UiCommand> Commands,
    bool IsLoading,
    string? Error);
