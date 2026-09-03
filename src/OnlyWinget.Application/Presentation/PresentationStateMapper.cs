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
            ToDashboardState(state),
            ToPresetsState(state),
            ToSearchState(state),
            ToUpdatesState(state),
            ToWindowsUpdateState(state),
            ToSourceState(state),
            ToActivityState(state));
    }

    public static DashboardPresentationState ToDashboardState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new DashboardPresentationState(
            state.Capabilities.IsWingetAvailable,
            state.Workspace.Presets.Count,
            state.ActivePreset?.Packages.Count ?? 0,
            state.SearchResults.Count,
            state.Updates.Count,
            state.Sources.Count,
            state.Capabilities.IsWindowsUpdateComAvailable,
            state.WindowsUpdates.Count,
            state.ActivePreset?.Name,
            state.LastWindowsUpdateResults.Any(result => result.RebootRequired),
            state.BusyState != ApplicationBusyState.Idle,
            state.UserVisibleError,
            state.Activity
                .TakeLast(5)
                .Reverse()
                .Select(entry => new ActivityRow(entry.Timestamp, entry.Timestamp.ToString("g", global::System.Globalization.CultureInfo.CurrentCulture), entry.Severity, entry.Title, entry.Message))
                .ToArray());
    }

    public static PresetsPresentationState ToPresetsState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
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
                    state.PackageMetadata.TryGetValue(package, out var metadata);
                    return new PresetPackageRow(
                        package.Id,
                        metadata?.Name,
                        package.Source,
                        metadata?.Version,
                        FormatPublisher(metadata),
                        state.SelectedPresetPackages.Contains(package));
                })
                .OrderBy(row => EmptyToNull(row.Name) ?? row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [],
            state.PresetInstallHeader,
            operationResults,
            [
                new(UiCommandId.AddPreset, "Command_Preset_Add", !isExecuting, UiCommandKind.Primary, Icon: "Add"),
                new(UiCommandId.RenamePreset, "Command_Preset_Rename", hasPreset && !isExecuting, Icon: "Edit"),
                new(UiCommandId.RemovePreset, "Command_Preset_Remove", hasPreset && !isExecuting, UiCommandKind.Destructive, ConfirmationResourceKey: "Dialog_RemovePreset_Message"),
                new(UiCommandId.AddPresetPackage, "Command_PresetPackage_Add", hasPreset && !isExecuting, UiCommandKind.Primary, Icon: "Add"),
                new(UiCommandId.EditPresetPackage, "Command_PresetPackage_Edit", state.SelectedPresetPackages.Count == 1 && !isExecuting, Icon: "Edit"),
                new(UiCommandId.RemovePresetPackages, "Command_PresetPackage_Remove", hasSelectedPackages && !isExecuting, UiCommandKind.Destructive),
                new(UiCommandId.ImportPreset, "Command_Preset_Import", !isExecuting, Placement: UiCommandPlacement.Overflow),
                new(UiCommandId.ExportPreset, "Command_Preset_Export", hasPreset && !isExecuting, Placement: UiCommandPlacement.Overflow),
                new(UiCommandId.SaveWorkspace, "Command_Workspace_Save", !isExecuting, Icon: "Save"),
                new(UiCommandId.InstallPreset, "Command_Preset_ApplyInstall", hasPackages && canUseWinget && !isExecuting, UiCommandKind.Primary, Icon: "Download"),
                new(UiCommandId.UninstallPreset, "Command_Preset_ApplyUninstall", hasPackages && canUseWinget && !isExecuting, UiCommandKind.Destructive, ConfirmationResourceKey: "Dialog_UninstallPreset_Message"),
                new(UiCommandId.CancelOperation, "Command_Operation_Cancel", isExecuting, UiCommandKind.Cancel, Icon: "Cancel")
            ],
            isExecuting,
            state.UserVisibleError);
    }

    public static SearchPresentationState ToSearchState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var isLoading = state.BusyState == ApplicationBusyState.Searching;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;
        var canUseWinget = state.Capabilities.CanUseWinget;

        return new SearchPresentationState(
            state.SearchResults
                .Select(result =>
                {
                    state.PackageMetadata.TryGetValue(result.Package, out var metadata);
                    return new SearchResultRow(
                        result.Package.Id,
                        result.Name,
                        result.Package.Source,
                        result.Version,
                        FormatPublisher(metadata),
                        result.Match,
                        state.SelectedSearchPackages.Contains(result.Package));
                })
                .OrderBy(row => EmptyToNull(row.Name) ?? row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            state.SearchSelectionHeader,
            [
                new(UiCommandId.SearchPackages, "Command_Search_Execute", canUseWinget && !isLoading && !isExecuting, UiCommandKind.Primary, Icon: "Find"),
                new(UiCommandId.InstallSearchResults, "Command_Search_InstallSelected", canUseWinget && state.SelectedSearchPackages.Count > 0 && !isLoading && !isExecuting, Icon: "Download"),
                new(UiCommandId.AddSearchResults, "Command_Search_AddSelected", canUseWinget && state.SelectedSearchPackages.Count > 0 && !isLoading && !isExecuting, Icon: "Add")
            ],
            isLoading,
            state.UserVisibleError);
    }

    public static UpdatesPresentationState ToUpdatesState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var isLoading = state.BusyState == ApplicationBusyState.RefreshingUpdates;
        var isExecuting = state.BusyState == ApplicationBusyState.ExecutingOperation;
        var canUseWinget = state.Capabilities.CanUseWinget;
        var operationResults = CreateOperationResultRows(state);
        var operationResultsByPackage = operationResults
            .GroupBy(result => new PackageIdentity(result.PackageId, result.Source))
            .ToDictionary(group => group.Key, group => group.Last());

        return new UpdatesPresentationState(
            state.Updates
                .Select(update =>
                {
                    operationResultsByPackage.TryGetValue(update.Package, out var result);
                    return new UpdateRow(
                        update.Package.Id,
                        update.Name,
                        update.Package.Source,
                        update.InstalledVersion,
                        update.AvailableVersion,
                        state.PackageMetadata.TryGetValue(update.Package, out var metadata)
                            ? FormatPublisher(metadata)
                            : "Value_Unknown",
                        state.SelectedUpdates.Contains(update.Package),
                        result?.Status ?? "Update_Status_Available",
                        result?.ErrorDetails,
                        result?.Output);
                })
                .OrderBy(row => EmptyToNull(row.Name) ?? row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            state.UpdatesSelectionHeader,
            operationResults,
            [
                new(UiCommandId.RefreshUpdates, "Command_Updates_Refresh", canUseWinget && !isLoading && !isExecuting, UiCommandKind.Primary, Icon: "Refresh"),
                new(UiCommandId.ApplyUpdates, "Command_Updates_ApplySelected", canUseWinget && state.SelectedUpdates.Count > 0 && !isLoading && !isExecuting, Icon: "Download"),
                new(UiCommandId.CancelOperation, "Command_Operation_Cancel", isExecuting, UiCommandKind.Cancel, Icon: "Cancel")
            ],
            isLoading,
            isExecuting,
            state.UserVisibleError);
    }

    public static WindowsUpdatePresentationState ToWindowsUpdateState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
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
                        result is null ? null : result.Succeeded ? "Operation_Status_Succeeded" : $"Operation_Status_Failed{(string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $" ({result.Message})")}",
                        result?.Message ?? (result?.RebootRequired == true ? "Restart required." : null));
                })
                .OrderBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UpdateId, StringComparer.OrdinalIgnoreCase)
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
                new(UiCommandId.ScanWindowsUpdates, "Command_WindowsUpdates_Scan", canUseWindowsUpdate && !isBusy, UiCommandKind.Primary, Icon: "Refresh"),
                new(UiCommandId.InstallWindowsUpdates, "Command_WindowsUpdates_InstallSelected", canUseWindowsUpdate && state.SelectedWindowsUpdates.Count > 0 && !isBusy, Icon: "Download"),
                new(UiCommandId.CancelOperation, "Command_Operation_Cancel", isBusy, UiCommandKind.Cancel, Icon: "Cancel")
            ],
            isScanning,
            isInstalling,
            state.UserVisibleError ?? (canUseWindowsUpdate ? null : state.Capabilities.WindowsUpdateUnavailableMessage));
    }

    public static SourcePresentationState ToSourceState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
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
                .OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [
                new(UiCommandId.RefreshSources, "Command_Sources_Refresh", canUseWinget && !isLoading, UiCommandKind.Primary, Icon: "Refresh"),
                new(UiCommandId.UpdateSources, "Command_Sources_Update", canUseWinget && !isLoading),
                new(UiCommandId.AddSource, "Command_Sources_Add", canUseWinget && !isLoading, Icon: "Add"),
                new(UiCommandId.RemoveSource, "Command_Sources_Remove", canUseWinget && hasSource && !isLoading, UiCommandKind.Destructive, ConfirmationResourceKey: "Dialog_RemoveSource_Message"),
                new(UiCommandId.ResetSources, "Command_Sources_Reset", canUseWinget && !isLoading, UiCommandKind.Destructive, UiCommandPlacement.Overflow, ConfirmationResourceKey: "Dialog_ResetSources_Message")
            ],
            isLoading,
            state.SourceError?.Message ?? state.UserVisibleError ?? (canUseWinget ? null : state.Capabilities.WingetUnavailableMessage));
    }

    public static ActivityPresentationState ToActivityState(OnlyWingetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new ActivityPresentationState(
            state.Activity
                .Select(entry => new ActivityRow(entry.Timestamp, entry.Timestamp.ToString("g", global::System.Globalization.CultureInfo.CurrentCulture), entry.Severity, entry.Title, entry.Message))
                .OrderByDescending(row => row.Timestamp)
                .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [
                new(UiCommandId.ExportActivity, "Command_Activity_Export", state.Activity.Count > 0, Icon: "Save"),
                new(UiCommandId.ClearActivity, "Command_Activity_Clear", state.Activity.Count > 0, UiCommandKind.Destructive, ConfirmationResourceKey: "Dialog_ClearActivity_Message")
            ]);
    }

    private static OperationResultRow[] CreateOperationResultRows(OnlyWingetState state) =>
        state.LastOperationResults
            .Select(result =>
            {
                var isWarning = result.Error?.Kind == WingetErrorKind.NoUpdates;
                var exitCode = result.CommandResult.ExitCode;
                var exitCodeSuffix = exitCode != 0
                    ? $" (Exit code: {exitCode} / 0x{exitCode:X8})"
                    : string.Empty;

                var errorDetails = result.Error?.Message ?? EmptyToNull(result.CommandResult.StandardError);
                if (errorDetails is not null && exitCode != 0)
                {
                    errorDetails = errorDetails + exitCodeSuffix;
                }
                else if (errorDetails is null && exitCode != 0)
                {
                    errorDetails = $"Command failed{exitCodeSuffix}";
                }

                return new OperationResultRow(
                    result.Selection.Package.Id,
                    result.Selection.Package.Source,
                    result.Selection.Action,
                    result.Succeeded,
                    isWarning,
                    isWarning ? "Operation_Status_Warning" : (result.Succeeded ? "Operation_Status_Succeeded" : "Operation_Status_Failed"),
                    errorDetails,
                    EmptyToNull(result.CommandResult.StandardOutput));
            })
            .ToArray();

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatPublisher(PackageResolution? metadata) =>
        string.IsNullOrWhiteSpace(metadata?.Publisher)
            ? "Value_Unknown"
            : metadata.Publisher;

    private static bool WindowsUpdateEquals(WindowsUpdateIdentity left, WindowsUpdateIdentity right) =>
        string.Equals(left.UpdateId, right.UpdateId, StringComparison.OrdinalIgnoreCase) &&
        left.RevisionNumber == right.RevisionNumber;

    private static string WindowsUpdateFingerprint(WindowsUpdateIdentity update) =>
        $"{update.UpdateId.ToUpperInvariant()}|{update.RevisionNumber}";
}
