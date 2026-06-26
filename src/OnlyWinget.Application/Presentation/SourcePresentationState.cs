namespace OnlyWinget.Application.Presentation;

public sealed record SourcePresentationState(
    IReadOnlyList<SourceRow> Sources,
    IReadOnlyList<PresentationCommand> Commands,
    bool IsLoading,
    string? Error);
