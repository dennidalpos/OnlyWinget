using OnlyWinget.Application.Activity;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

/// <summary>History, hide/show, uninstall and restart: the Windows Settings > Windows Update features beyond scan/install.</summary>
public sealed partial class OnlyWingetApplication
{
    private const int DefaultHistoryEntries = 500;

    private readonly List<WindowsUpdateHistoryEntry> windowsUpdateHistory = [];
    private readonly SelectionState<WindowsUpdateIdentity> historySelection = new();
    private bool restartPending;

    public bool CanManageWindowsUpdates => updateManagement is not null;

    public async Task<ApplicationActionResult> LoadWindowsUpdateHistoryAsync(
        CancellationToken callerCancellationToken,
        int maxEntries = DefaultHistoryEntries)
    {
        return await RunAsync(
                ApplicationBusyState.LoadingWindowsUpdateHistory,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    var management = RequireManagement();
                    var outcome = await management.GetHistoryAsync(maxEntries, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update history could not be read.");
                    }

                    UpdateState(() =>
                    {
                        windowsUpdateHistory.Clear();
                        windowsUpdateHistory.AddRange(outcome.Rows);
                        historySelection.ReplaceAvailable(outcome.Rows
                            .Where(CanUninstallFromHistory)
                            .Select(entry => new WindowsUpdateIdentity(entry.UpdateId!, 0)));
                    });
                },
                "Unable to read the Windows Update history.")
            .ConfigureAwait(false);
    }

    /// <summary>An install that succeeded and has an identity can be offered for uninstall; WUA decides if it is allowed.</summary>
    internal static bool CanUninstallFromHistory(WindowsUpdateHistoryEntry entry) =>
        entry.Operation == WindowsUpdateHistoryOperation.Install
        && entry.Result == WindowsUpdateHistoryResult.Succeeded
        && !string.IsNullOrWhiteSpace(entry.UpdateId)
        && entry.Classification != WindowsUpdateClassification.Definition;

    public ApplicationActionResult ToggleAllHistoryUpdates() => Run(historySelection.ToggleAll);

    public ApplicationActionResult SetHistoryUpdatesSelection(IEnumerable<WindowsUpdateIdentity> updates, bool isSelected) =>
        Run(() => { foreach (var update in updates) historySelection.SetSelected(update, isSelected); });

    public async Task<ApplicationActionResult> HideSelectedWindowsUpdatesAsync(
        bool hidden,
        WindowsUpdateOptions options,
        CancellationToken callerCancellationToken)
    {
        var selected = ReadState(() => windowsUpdateSelection.Selected.ToArray());
        return await RunAsync(
                ApplicationBusyState.ManagingWindowsUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    var management = RequireManagement();
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one Windows update first.");
                    }

                    var outcome = await management.SetHiddenAsync(selected, hidden, options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update could not change the hidden state.");
                    }

                    ReportManagementResults(outcome.Rows, hidden ? "Windows update hidden" : "Windows update shown");
                    ThrowIfAnyFailed(outcome.Rows);
                },
                hidden ? "Unable to hide the selected Windows updates." : "Unable to show the selected Windows updates.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> UninstallHistoryUpdatesAsync(
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var selected = ReadState(() => historySelection.Selected.ToArray());
        return await RunAsync(
                ApplicationBusyState.ManagingWindowsUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    var management = RequireManagement();
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one installed Windows update to uninstall.");
                    }

                    UpdateState(lastWindowsUpdateResults.Clear);
                    AddActivity(ActivitySeverity.Information, "Windows Update uninstall started", $"{selected.Length} update(s).");
                    var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                    {
                        UpdateState(() => operationProgress = update);
                        progress?.Report(update);
                        NotifyStateChanged();
                    });
                    var outcome = await management.UninstallAsync(selected, cancellationToken, forwardingProgress).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update uninstall failed.");
                    }

                    UpdateState(() => lastWindowsUpdateResults.AddRange(outcome.Rows));
                    ReportManagementResults(outcome.Rows, "Windows update uninstalled");
                    if (outcome.Rows.Any(row => row.RebootRequired))
                    {
                        AddActivity(ActivitySeverity.Information, "Restart required", "Restart Windows to finish removing the update.");
                    }

                    RefreshRestartPending();
                    ThrowIfAnyFailed(outcome.Rows);
                },
                "Unable to uninstall the selected Windows updates.")
            .ConfigureAwait(false);
    }

    public bool IsRestartPending() => restartService?.IsRestartPending() ?? false;

    /// <summary>Re-reads the pending-restart marker so the UI can offer "Restart now".</summary>
    public void RefreshRestartPending()
    {
        var pending = IsRestartPending();
        UpdateState(() => restartPending = pending);
        NotifyStateChanged();
    }

    public async Task<ApplicationActionResult> ScheduleRestartAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (restartService is null)
        {
            return ApplicationActionResult.Failure("Restart is not available on this system.");
        }

        var scheduled = await restartService.ScheduleRestartAsync(delay, cancellationToken).ConfigureAwait(false);
        AddActivity(
            scheduled ? ActivitySeverity.Warning : ActivitySeverity.Error,
            scheduled ? "Restart scheduled" : "Restart failed",
            scheduled
                ? $"Windows will restart in {(int)delay.TotalSeconds} second(s). Cancel it from the Windows Update page."
                : "Windows did not accept the restart request.");
        return scheduled ? ApplicationActionResult.Success : ApplicationActionResult.Failure("Windows did not accept the restart request.");
    }

    public async Task<ApplicationActionResult> CancelRestartAsync(CancellationToken cancellationToken)
    {
        if (restartService is null)
        {
            return ApplicationActionResult.Failure("Restart is not available on this system.");
        }

        var cancelled = await restartService.CancelRestartAsync(cancellationToken).ConfigureAwait(false);
        AddActivity(
            cancelled ? ActivitySeverity.Information : ActivitySeverity.Warning,
            cancelled ? "Restart cancelled" : "No restart to cancel",
            cancelled ? "The scheduled restart was cancelled." : "No restart was scheduled.");
        return cancelled ? ApplicationActionResult.Success : ApplicationActionResult.Failure("No restart was scheduled.");
    }

    private IWindowsUpdateManagementService RequireManagement() =>
        updateManagement ?? throw new InvalidOperationException("Windows Update management is not available on this system.");

    private void ReportManagementResults(IReadOnlyList<WindowsUpdateInstallResult> rows, string successTitle)
    {
        foreach (var row in rows)
        {
            AddActivity(
                row.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error,
                row.Succeeded ? successTitle : row.Title,
                row.Succeeded ? row.Title : $"{row.Message} (Result Code: {row.ResultCode})");
        }
    }

    private static void ThrowIfAnyFailed(IReadOnlyList<WindowsUpdateInstallResult> rows)
    {
        var failed = rows.Where(row => !row.Succeeded).ToArray();
        if (failed.Length > 0)
        {
            throw new InvalidOperationException($"One or more Windows updates failed: {string.Join(", ", failed.Select(row => row.Title))}");
        }
    }
}
