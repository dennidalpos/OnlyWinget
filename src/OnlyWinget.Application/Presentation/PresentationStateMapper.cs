using OnlyWinget.Application.App;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Presentation;

public static class PresentationStateMapper
{
    public static OnlyWingetPresentationState FromApplicationState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new OnlyWingetPresentationState(
            CreateDashboardState(state),
            CreatePresetsState(state),
            CreateSearchState(state),
            CreateUpdatesState(state),
            CreateWindowsUpdateState(state),
            CreateSourceState(state),
            CreateActivityState(state));
    }

    private static DashboardPresentationState CreateDashboardState(OnlyWingetState state)
    {
        return new DashboardPresentationState(
            state.Capabilities.IsWingetAvailable,
            state.Workspace.Presets.Count,
            state.ActivePreset?.Packages.Count ?? 0,
            state.SearchResults.Count,
            state.Updates.Count,
            state.Sources.Count,
            state.BusyState != ApplicationBusyState.Idle,
            state.UserVisibleError,
            state.Activity
                .TakeLast(5)
                .Reverse()
                .Select(entry => new ActivityRow(entry.Timestamp, entry.Severity, entry.Title, entry.Message))
                .ToArray());
    }

    private static PresetsPresentationState CreatePresetsState(OnlyWingetState state)
    {
        var active = state.ActivePreset;
        var hasPreset = active is not null;
        var hasPackages = active?.Packages.Count > 0;
        var hasSelectedPackages = state.SelectedPresetPackages.Count > 0;
        var isExecuting = state.BusyState is ApplicationBusyState.ExecutingOperation or ApplicationBusyState.ValidatingPackages;
        var canUseWinget = state.Capabilities.CanUseWinget;
        var operationResults = CreateOperationResultRows(state);

        return new PresetsPresentationState(
            state.Workspace.Presets.Select(preset => preset.Name).ToArray(),
            active?.Name,
            active?.Packages
                .Select(package =>
                {
                    state.PackageMetadata.TryGetValue(PackageFingerprint(package), out var metadata);
                    return new PresetPackageRow(
                        package.Id,
                        metadata?.Name,
                        package.Source,
                        metadata?.Version,
                        FormatArchitectures(metadata),
                        state.SelectedPresetPackages.Any(selected => PackageEquals(selected, package)));
                })
                .ToArray() ?? [],
            state.PresetSelectionHeader,
            operationResults,
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
                new("preset.apply.install", "Command_Preset_ApplyInstall", hasPackages && canUseWinget && !isExecuting),
                new("preset.apply.uninstall", "Command_Preset_ApplyUninstall", hasPackages && canUseWinget && !isExecuting),
                new("operation.cancel", "Command_Operation_Cancel", isExecuting)
            ],
            isExecuting,
            state.UserVisibleError);
    }

    private static SearchPresentationState CreateSearchState(OnlyWingetState state)
    {
        var isLoading = state.BusyState == ApplicationBusyState.Searching;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;
        var canUseWinget = state.Capabilities.CanUseWinget;

        return new SearchPresentationState(
            state.SearchResults
                .Select(result =>
                {
                    state.PackageMetadata.TryGetValue(PackageFingerprint(result.Package), out var metadata);
                    return new SearchResultRow(
                        result.Package.Id,
                        result.Name,
                        result.Package.Source,
                        result.Version,
                        FormatArchitectures(metadata),
                        result.Match,
                        state.SelectedSearchPackages.Any(selected => PackageEquals(selected, result.Package)));
                })
                .ToArray(),
            state.SearchSelectionHeader,
            [
                new("search.execute", "Command_Search_Execute", canUseWinget && !isLoading && !isExecuting),
                new("search.addSelected", "Command_Search_AddSelected", canUseWinget && state.SelectedSearchPackages.Count > 0 && !isLoading && !isExecuting)
            ],
            isLoading,
            state.UserVisibleError);
    }

    private static UpdatesPresentationState CreateUpdatesState(OnlyWingetState state)
    {
        var isLoading = state.BusyState == ApplicationBusyState.RefreshingUpdates;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;
        var canUseWinget = state.Capabilities.CanUseWinget;
        var operationResults = CreateOperationResultRows(state);
        var operationResultsByPackage = operationResults
            .GroupBy(result => PackageFingerprint(result.PackageId, result.Source), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        return new UpdatesPresentationState(
            state.Updates
                .Select(update =>
                {
                    operationResultsByPackage.TryGetValue(PackageFingerprint(update.Package), out var result);
                    return new UpdateRow(
                        update.Package.Id,
                        update.Name,
                        update.Package.Source,
                        update.InstalledVersion,
                        update.AvailableVersion,
                        state.PackageMetadata.TryGetValue(PackageFingerprint(update.Package), out var metadata)
                            ? FormatArchitectures(metadata)
                            : "Value_Unknown",
                        state.SelectedUpdates.Any(selected => PackageEquals(selected, update.Package)),
                        result?.Status ?? "Update_Status_Available",
                        result?.ErrorDetails,
                        result?.Output);
                })
                .ToArray(),
            state.UpdatesSelectionHeader,
            operationResults,
            [
                new("updates.refresh", "Command_Updates_Refresh", canUseWinget && !isLoading && !isExecuting),
                new("updates.applySelected", "Command_Updates_ApplySelected", canUseWinget && state.SelectedUpdates.Count > 0 && !isLoading && !isExecuting),
                new("operation.cancel", "Command_Operation_Cancel", isExecuting)
            ],
            isLoading,
            isExecuting,
            state.UserVisibleError);
    }

    private static WindowsUpdatePresentationState CreateWindowsUpdateState(OnlyWingetState state)
    {
        var isScanning = state.BusyState == ApplicationBusyState.ScanningWindowsUpdates;
        var isInstalling = state.BusyState == ApplicationBusyState.InstallingWindowsUpdates;
        var isBusy = isScanning || isInstalling;
        var canUseWindowsUpdate = state.Capabilities.CanUseWindowsUpdate;
        var resultsByUpdate = state.LastWindowsUpdateResults
            .GroupBy(result => WindowsUpdateFingerprint(result.Identity), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        return new WindowsUpdatePresentationState(
            state.WindowsUpdates
                .Select(update =>
                {
                    resultsByUpdate.TryGetValue(WindowsUpdateFingerprint(update.Identity), out var result);
                    return new WindowsUpdateRow(
                        update.Identity.UpdateId,
                        update.Identity.RevisionNumber,
                        update.Title,
                        update.Description,
                        update.Severity,
                        string.Join(", ", update.Categories),
                        string.Join(", ", update.KnowledgeBaseArticles.Select(article => $"KB{article}")),
                        update.MaxDownloadSize,
                        update.IsDownloaded,
                        update.RebootRequired,
                        state.SelectedWindowsUpdates.Any(selected => WindowsUpdateEquals(selected, update.Identity)),
                        result is null ? null : result.Succeeded ? "Succeeded" : "Failed",
                        result?.Message ?? (result?.RebootRequired == true ? "Restart required." : null));
                })
                .ToArray(),
            state.WindowsUpdatesSelectionHeader,
            state.LastWindowsUpdateResults
                .Select(result => new WindowsUpdateResultRow(
                    result.Identity.UpdateId,
                    result.Identity.RevisionNumber,
                    result.Title,
                    result.Succeeded,
                    result.RebootRequired,
                    result.ResultCode,
                    result.Message))
                .ToArray(),
            [
                new("windowsUpdates.scan", "Command_WindowsUpdates_Scan", canUseWindowsUpdate && !isBusy),
                new("windowsUpdates.installSelected", "Command_WindowsUpdates_InstallSelected", canUseWindowsUpdate && state.SelectedWindowsUpdates.Count > 0 && !isBusy),
                new("operation.cancel", "Command_Operation_Cancel", isBusy)
            ],
            isScanning,
            isInstalling,
            state.UserVisibleError ?? (canUseWindowsUpdate ? null : state.Capabilities.WindowsUpdateUnavailableMessage));
    }

    private static SourcePresentationState CreateSourceState(OnlyWingetState state)
    {
        var isLoading = state.BusyState is ApplicationBusyState.ManagingSources or ApplicationBusyState.CheckingCapabilities;
        var hasSource = state.Sources.Count > 0;
        var canUseWinget = state.Capabilities.CanUseWinget;

        return new SourcePresentationState(
            state.Sources
                .Select(source => new SourceRow(
                    source.Name,
                    source.Argument,
                    source.IsExplicit,
                    source.IsExplicit ? "Source_Type_User" : "Source_Type_Default",
                    source.Status.ToString(),
                    source.IsEnabled))
                .ToArray(),
            [
                new("sources.refresh", "Command_Sources_Refresh", canUseWinget && !isLoading),
                new("sources.update", "Command_Sources_Update", canUseWinget && !isLoading),
                new("sources.add", "Command_Sources_Add", canUseWinget && !isLoading),
                new("sources.remove", "Command_Sources_Remove", canUseWinget && hasSource && !isLoading),
                new("sources.reset", "Command_Sources_Reset", canUseWinget && !isLoading)
            ],
            isLoading,
            state.SourceError?.Message ?? state.UserVisibleError ?? (canUseWinget ? null : state.Capabilities.WingetUnavailableMessage));
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

    private static OperationResultRow[] CreateOperationResultRows(OnlyWingetState state) =>
        state.LastOperationResults
            .Select(result => new OperationResultRow(
                result.Selection.Package.Id,
                result.Selection.Package.Source,
                result.Selection.Action,
                result.Succeeded,
                result.Succeeded ? "Succeeded" : "Failed",
                result.Error?.Message ?? EmptyToNull(result.CommandResult.StandardError),
                EmptyToNull(result.CommandResult.StandardOutput)))
            .ToArray();

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatArchitectures(PackageResolution? metadata) =>
        metadata?.Architectures is { Count: > 0 } architectures
            ? string.Join(", ", architectures)
            : "Architecture_Automatic";

    private static bool PackageEquals(PackageIdentity left, PackageIdentity right) =>
        string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Source ?? string.Empty, right.Source ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string PackageFingerprint(PackageIdentity package) =>
        PackageFingerprint(package.Id, package.Source);

    private static string PackageFingerprint(string packageId, string? source) =>
        $"{source?.ToUpperInvariant() ?? string.Empty}|{packageId.ToUpperInvariant()}";

    private static bool WindowsUpdateEquals(WindowsUpdateIdentity left, WindowsUpdateIdentity right) =>
        string.Equals(left.UpdateId, right.UpdateId, StringComparison.OrdinalIgnoreCase) &&
        left.RevisionNumber == right.RevisionNumber;

    private static string WindowsUpdateFingerprint(WindowsUpdateIdentity update) =>
        $"{update.UpdateId.ToUpperInvariant()}|{update.RevisionNumber}";
}
