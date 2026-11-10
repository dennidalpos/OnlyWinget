namespace OnlyWinget.Application.Presentation;

public sealed record DashboardPresentationState(
    bool? IsWingetAvailable,
    int PresetCount,
    int ActivePresetPackageCount,
    int SearchResultCount,
    int UpdateCount,
    int SourceCount,
    bool? IsWindowsUpdateAvailable,
    int WindowsUpdateCount,
    string? ActivePresetName,
    bool RebootRequired,
    bool IsBusy,
    string? Error,
    IReadOnlyList<ActivityRow> RecentActivity);
