using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.Presentation;

public sealed record WindowsUpdateHistoryRow(
    string UpdateId,
    DateTimeOffset Date,
    string Title,
    string KnowledgeBase,
    WindowsUpdateClassification Classification,
    string ClassificationKey,
    WindowsUpdateHistoryOperation Operation,
    WindowsUpdateHistoryResult Result,
    string? ErrorDetail,
    bool CanSelect,
    bool IsSelected);

public sealed record WindowsUpdateHistoryPresentationState(
    IReadOnlyList<WindowsUpdateHistoryRow> Rows,
    SelectionHeaderState HeaderState,
    IReadOnlyList<UiCommand> Commands,
    bool IsLoading,
    bool IsManaging,
    string? Error);
