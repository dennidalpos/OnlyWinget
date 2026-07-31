using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

public sealed record OnlyWingetState(
    WorkspaceState Workspace,
    Preset? ActivePreset,
    IReadOnlyList<PackageIdentity> SelectedPresetPackages,
    SelectionHeaderState PresetInstallHeader,
    IReadOnlyList<PackageSearchResult> SearchResults,
    IReadOnlyList<PackageIdentity> SelectedSearchPackages,
    SelectionHeaderState SearchSelectionHeader,
    IReadOnlyList<PackageUpdate> Updates,
    IReadOnlyList<PackageIdentity> SelectedUpdates,
    SelectionHeaderState UpdatesSelectionHeader,
    IReadOnlyList<WindowsUpdateItem> WindowsUpdates,
    IReadOnlyList<WindowsUpdateIdentity> SelectedWindowsUpdates,
    SelectionHeaderState WindowsUpdatesSelectionHeader,
    IReadOnlyList<WindowsUpdateInstallResult> LastWindowsUpdateResults,
    IReadOnlyDictionary<PackageIdentity, PackageResolution> PackageMetadata,
    SystemCapabilities Capabilities,
    IReadOnlyList<WingetSource> Sources,
    ClassifiedWingetError? SourceError,
    IReadOnlyList<ActivityEntry> Activity,
    IReadOnlyList<OperationExecutionResult> LastOperationResults,
    OperationProgress? OperationProgress,
    ApplicationBusyState BusyState,
    string? UserVisibleError);
