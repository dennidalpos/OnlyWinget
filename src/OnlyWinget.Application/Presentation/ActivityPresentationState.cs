namespace OnlyWinget.Application.Presentation;

public sealed record ActivityPresentationState(
    IReadOnlyList<ActivityRow> Entries,
    IReadOnlyList<PresentationCommand> Commands);
