namespace OnlyWinget.Application.Presentation;

public sealed record OnlyWingetPresentationState(
    DashboardPresentationState Dashboard,
    PresetsPresentationState Presets,
    SearchPresentationState Search,
    UpdatesPresentationState Updates,
    WindowsUpdatePresentationState WindowsUpdates,
    SourcePresentationState Sources,
    ActivityPresentationState Activity);
