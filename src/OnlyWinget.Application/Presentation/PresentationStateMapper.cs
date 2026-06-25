using OnlyWinget.Application.App;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Presentation;

public static class PresentationStateMapper
{
    public static OnlyWingetPresentationState FromApplicationState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new OnlyWingetPresentationState(
            CreatePresetsState(state),
            CreateSearchState(state),
            CreateUpdatesState(state),
            CreateActivityState(state));
    }

    private static PresetsPresentationState CreatePresetsState(OnlyWingetState state)
    {
        var active = state.ActivePreset;
        var hasPreset = active is not null;
        var hasPackages = active?.Packages.Count > 0;
        var hasSelectedPackages = state.SelectedPresetPackages.Count > 0;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;

        return new PresetsPresentationState(
            state.Workspace.Presets.Select(preset => preset.Name).ToArray(),
            active?.Name,
            active?.Packages
                .Select(package => new PresetPackageRow(
                    package.Id,
                    package.Source,
                    state.SelectedPresetPackages.Any(selected => PackageEquals(selected, package))))
                .ToArray() ?? [],
            state.PresetSelectionHeader,
            [
                new("preset.add", "Command_Preset_Add", !isExecuting),
                new("preset.rename", "Command_Preset_Rename", hasPreset && !isExecuting),
                new("preset.remove", "Command_Preset_Remove", hasPreset && !isExecuting),
                new("preset.package.add", "Command_PresetPackage_Add", hasPreset && !isExecuting),
                new("preset.package.edit", "Command_PresetPackage_Edit", state.SelectedPresetPackages.Count == 1 && !isExecuting),
                new("preset.package.remove", "Command_PresetPackage_Remove", hasSelectedPackages && !isExecuting),
                new("preset.import", "Command_Preset_Import", !isExecuting),
                new("preset.export", "Command_Preset_Export", hasPreset && !isExecuting),
                new("preset.save", "Command_Workspace_Save", !isExecuting),
                new("preset.apply.install", "Command_Preset_ApplyInstall", hasPackages && !isExecuting),
                new("preset.apply.uninstall", "Command_Preset_ApplyUninstall", hasPackages && !isExecuting)
            ],
            state.UserVisibleError);
    }

    private static SearchPresentationState CreateSearchState(OnlyWingetState state)
    {
        var isLoading = state.BusyState == ApplicationBusyState.Searching;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;

        return new SearchPresentationState(
            state.SearchResults
                .Select(result => new SearchResultRow(
                    result.Package.Id,
                    result.Name,
                    result.Package.Source,
                    result.Version,
                    result.Match,
                    state.SelectedSearchPackages.Any(selected => PackageEquals(selected, result.Package))))
                .ToArray(),
            state.SearchSelectionHeader,
            [
                new("search.execute", "Command_Search_Execute", !isLoading && !isExecuting),
                new("search.addSelected", "Command_Search_AddSelected", state.SelectedSearchPackages.Count > 0 && state.ActivePreset is not null && !isLoading && !isExecuting)
            ],
            isLoading,
            state.UserVisibleError);
    }

    private static UpdatesPresentationState CreateUpdatesState(OnlyWingetState state)
    {
        var isLoading = state.BusyState == ApplicationBusyState.RefreshingUpdates;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;

        return new UpdatesPresentationState(
            state.Updates
                .Select(update => new UpdateRow(
                    update.Package.Id,
                    update.Name,
                    update.Package.Source,
                    update.InstalledVersion,
                    update.AvailableVersion,
                    state.SelectedUpdates.Any(selected => PackageEquals(selected, update.Package))))
                .ToArray(),
            state.UpdatesSelectionHeader,
            [
                new("updates.refresh", "Command_Updates_Refresh", !isLoading && !isExecuting),
                new("updates.applySelected", "Command_Updates_ApplySelected", state.SelectedUpdates.Count > 0 && !isLoading && !isExecuting),
                new("operation.cancel", "Command_Operation_Cancel", isExecuting)
            ],
            isLoading,
            state.UserVisibleError);
    }

    private static ActivityPresentationState CreateActivityState(OnlyWingetState state)
    {
        return new ActivityPresentationState(
            state.Activity
                .Select(entry => new ActivityRow(entry.Timestamp, entry.Severity, entry.Title, entry.Message))
                .ToArray(),
            [
                new("activity.clear", "Command_Activity_Clear", state.Activity.Count > 0)
            ]);
    }

    private static bool PackageEquals(PackageIdentity left, PackageIdentity right) =>
        string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Source ?? string.Empty, right.Source ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
