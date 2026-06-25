namespace OnlyWinget.Application.Presentation;

public sealed record OnlyWingetPresentationState(
    PresetsPresentationState Presets,
    SearchPresentationState Search,
    UpdatesPresentationState Updates,
    ActivityPresentationState Activity);
