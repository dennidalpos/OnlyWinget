using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

public sealed record OnlyWingetState(
    WorkspaceState Workspace,
    Preset? ActivePreset,
    IReadOnlyList<PackageIdentity> SelectedPresetPackages,
    SelectionHeaderState PresetSelectionHeader,
    IReadOnlyList<PackageSearchResult> SearchResults,
    IReadOnlyList<PackageIdentity> SelectedSearchPackages,
    SelectionHeaderState SearchSelectionHeader,
    IReadOnlyList<PackageUpdate> Updates,
    IReadOnlyList<PackageIdentity> SelectedUpdates,
    SelectionHeaderState UpdatesSelectionHeader,
    IReadOnlyList<ActivityEntry> Activity,
    IReadOnlyList<OperationExecutionResult> LastOperationResults,
    ApplicationBusyState BusyState,
    string? UserVisibleError);
