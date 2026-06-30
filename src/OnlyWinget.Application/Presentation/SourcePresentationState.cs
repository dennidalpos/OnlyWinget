namespace OnlyWinget.Application.Presentation;

public sealed record SourcePresentationState(
    IReadOnlyList<SourceRow> Sources,
    IReadOnlyList<UiCommand> Commands,
    bool IsLoading,
    string? Error);
