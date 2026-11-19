using OnlyWinget.Application.Activity;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> RefreshUpdatesAsync(CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.RefreshingUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    var enabledSources = GetEnabledSourceNames();
                    if (enabledSources.Count == 0)
                    {
                        throw new InvalidOperationException("Enable at least one winget source before refreshing updates.");
                    }

                    var outcomes = await Task.WhenAll(enabledSources.Select(async source =>
                    {
                        var outcome = await updateLoader.LoadUpdatesAsync(source, cancellationToken).ConfigureAwait(false);
                        return (Source: source, Outcome: outcome);
                    })).ConfigureAwait(false);
                    var sourceErrors = outcomes
                        .Where(item => !item.Outcome.Succeeded && item.Outcome.Error?.Kind != WingetErrorKind.NoUpdates)
                        .Select(item => $"{item.Source}: {item.Outcome.Error?.Message ?? "winget upgrade failed."}")
                        .ToArray();
                    var distinctUpdates = outcomes.Where(item => item.Outcome.Succeeded)
                        .SelectMany(item => item.Outcome.Rows)
                        .DistinctBy(update => update.Package)
                        .ToArray();

                    if (distinctUpdates.Length == 0 && sourceErrors.Length > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    await RefreshPackageMetadataAsync(
                            distinctUpdates.Select(update => update.Package),
                            cancellationToken)
                        .ConfigureAwait(false);

                    var unreadableSources = outcomes
                        .Where(item => item.Outcome.Error?.Kind == WingetErrorKind.InvalidOutput)
                        .Select(item => item.Source)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    UpdateState(() =>
                    {
                        var refreshedUpdates = distinctUpdates
                            .Concat(updates.Where(update =>
                                update.Package.Source is { } source && unreadableSources.Contains(source)))
                            .DistinctBy(update => update.Package)
                            .OrderBy(update => update.Name, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        updates.Clear();
                        updates.AddRange(refreshedUpdates);
                        updateSelection.ReplaceAvailable(refreshedUpdates.Select(update => update.Package));
                    });
                    AddActivity(ActivityCategory.Packages, ActivitySeverity.Information, "Updates refreshed", $"{distinctUpdates.Length} update(s).");

                    if (sourceErrors.Length > 0)
                    {
                        AddActivity(
                            ActivityCategory.Packages, ActivitySeverity.Warning,
                            "Some sources could not be refreshed",
                            string.Join(Environment.NewLine, sourceErrors));
                    }
                },
                "Unable to refresh updates.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleUpdate(PackageIdentity package) => ToggleSelection(updateSelection, package);

    public ApplicationActionResult ToggleAllUpdates() => Run(updateSelection.ToggleAll);

    public ApplicationActionResult SetUpdatesSelection(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) updateSelection.SetSelected(p, isSelected); });

    public Task<ApplicationActionResult> ApplySelectedUpdatesAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        ApplyUpdatesAsync(false, cancellationToken, progress);

    public Task<ApplicationActionResult> ApplyAllUpdatesAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        ApplyUpdatesAsync(true, cancellationToken, progress);

    private Task<ApplicationActionResult> ApplyUpdatesAsync(
        bool all,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress)
    {
        var selections = ReadState(() => all
            ? updates.Select(update => new PackageSelection(update.Package, PackageAction.Upgrade, update.Name)).ToArray()
            : updateSelection.Selected.Select(package => new PackageSelection(package, PackageAction.Upgrade,
                updates.FirstOrDefault(update => update.Package.Equals(package))?.Name)).ToArray());
        return ExecutePlanAsync(new OperationPlan(all ? "All updates" : "Selected updates", selections),
            cancellationToken, progress);
    }

    /// <summary>Packages whose last upgrade failed because winget cannot upgrade across installer technologies.</summary>
    public static IReadOnlySet<PackageIdentity> PackagesNeedingReinstall(OnlyWingetState state) =>
        state.LastOperationResults
            .Where(result => result.Selection.Action == PackageAction.Upgrade
                && result.Error?.Kind == WingetErrorKind.InstallTechnologyMismatch)
            .Select(result => result.Selection.Package)
            .Where(package => state.Updates.Any(update => update.Package.Equals(package)))
            .ToHashSet();

    /// <summary>
    /// Replaces selected apps that winget cannot upgrade in place: uninstall the installed copy, then install the
    /// winget version. The install only runs after a successful uninstall; the caller confirms with the user first.
    /// </summary>
    public async Task<ApplicationActionResult> ReinstallSelectedUpdatesAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var targets = ReadState(() =>
        {
            var needing = PackagesNeedingReinstall(State);
            return updates
                .Where(update => updateSelection.Selected.Contains(update.Package) && needing.Contains(update.Package))
                .ToArray();
        });
        if (targets.Length == 0)
        {
            return Run(() => throw new InvalidOperationException(UserText("Error_SelectForReinstall", "Select an update that needs reinstalling first.")));
        }

        ApplicationActionResult? last = null;
        foreach (var target in targets)
        {
            var uninstall = await ExecutePlanAsync(
                new OperationPlan($"Reinstall {target.Package.Id}: uninstall",
                    [new PackageSelection(target.Package, PackageAction.Uninstall, target.Name)]),
                cancellationToken, progress).ConfigureAwait(false);
            last = uninstall;
            if (!uninstall.Succeeded)
            {
                AddActivity(ActivityCategory.Packages, ActivitySeverity.Warning, target.Package.Id, UserText("Activity_ReinstallUninstallFailed", "Uninstall did not complete; the app was not reinstalled."));
                continue;
            }

            // winget exits as soon as a "remove only" uninstaller has started; do not install over the old copy.
            var removal = await WaitForRemovalAsync(target.Package, cancellationToken).ConfigureAwait(false);
            if (!removal.Succeeded)
            {
                last = removal;
                continue;
            }

            last = await ExecutePlanAsync(
                new OperationPlan($"Reinstall {target.Package.Id}: install",
                    [new PackageSelection(target.Package, PackageAction.Install)]),
                cancellationToken, progress).ConfigureAwait(false);
            if (!last.Succeeded)
            {
                AddActivity(ActivityCategory.Packages, ActivitySeverity.Error, target.Package.Id,
                    UserText("Activity_ReinstallInstallFailed", "The previous version was uninstalled but the new one failed to install. Install it again from Packages."));
            }
        }

        return last!;
    }

    private Task<ApplicationActionResult> WaitForRemovalAsync(PackageIdentity package, CancellationToken cancellationToken) =>
        RunAsync(
            ApplicationBusyState.ExecutingOperation,
            cancellationToken,
            async token =>
            {
                var waited = global::System.Diagnostics.Stopwatch.StartNew();
                var announced = false;
                while (true)
                {
                    var status = await packageResolver.CheckInstalledStatusAsync(package, token).ConfigureAwait(false);
                    if (!status.IsInstalled)
                    {
                        return;
                    }

                    if (!announced)
                    {
                        announced = true;
                        AddActivity(ActivityCategory.Packages, ActivitySeverity.Information, package.Id,
                            UserText("Activity_ReinstallWaiting", "The uninstaller is still running. Finish its window; the new version installs as soon as the old one is gone."));
                    }

                    if (waited.Elapsed > TimeSpan.FromMinutes(10))
                    {
                        throw new InvalidOperationException(
                            FormatUserText("Error_ReinstallStillInstalled", "{0} is still installed. Finish or close its uninstaller, then try again.", package.Id));
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                }
            },
            UserText("Error_ReinstallRemovalUnconfirmed", "Unable to confirm that the app was uninstalled."));

    public async Task<ApplicationActionResult> ScanWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ScanningWindowsUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    var outcome = await windowsUpdateService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? UserText("Error_WindowsScanFailed", "Windows Update scan failed."));
                    }

                    UpdateState(() =>
                    {
                        windowsUpdates.Clear();
                        windowsUpdates.AddRange(outcome.Rows
                        .DistinctBy(update => WindowsUpdateFingerprint(update.Identity))
                        .OrderBy(update => update.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(update => update.Identity.UpdateId, StringComparer.OrdinalIgnoreCase));
                        windowsUpdateSelection.ReplaceAvailable(windowsUpdates.Select(update => update.Identity));
                    });
                    AddActivity(ActivityCategory.WindowsUpdate, ActivitySeverity.Information, UserText("Activity_WindowsScanCompleted", "Windows Update scan completed"), FormatUserText("Activity_WindowsUpdateCount", "{0} update(s).", ReadState(() => windowsUpdates.Count)));
                },
                "Unable to scan Windows Update.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleWindowsUpdate(WindowsUpdateIdentity update) =>
        ToggleSelection(windowsUpdateSelection, update);

    public ApplicationActionResult ToggleAllWindowsUpdates() => Run(windowsUpdateSelection.ToggleAll);

    public ApplicationActionResult SetWindowsUpdatesSelection(IEnumerable<WindowsUpdateIdentity> updates, bool isSelected) =>
        Run(() => { foreach (var u in updates) windowsUpdateSelection.SetSelected(u, isSelected); });

    public Task<ApplicationActionResult> InstallSelectedWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        InstallWindowsUpdatesAsync(options, false, callerCancellationToken, progress);

    public Task<ApplicationActionResult> InstallAllWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        InstallWindowsUpdatesAsync(options with { DownloadOnly = false, ShowHidden = false }, true, callerCancellationToken, progress);

    private async Task<ApplicationActionResult> InstallWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        bool all,
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress)
    {
        return await RunAsync(
                ApplicationBusyState.InstallingWindowsUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    var selected = ReadState(() => all
                        ? windowsUpdates.Where(update => !update.IsHidden).Select(update => update.Identity).ToArray()
                        : windowsUpdateSelection.Selected.ToArray());
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException(all
                            ? UserText("Error_NoVisibleWindowsUpdates", "Scan for available Windows updates before installing all.")
                            : UserText("Error_SelectWindowsUpdates", "Select at least one Windows update before installing."));
                    }

                    UpdateState(lastWindowsUpdateResults.Clear);
                    AddActivity(ActivityCategory.WindowsUpdate, ActivitySeverity.Information, UserText("Activity_WindowsInstallStarted", "Windows Update install started"), FormatUserText("Activity_WindowsUpdateCount", "{0} update(s).", selected.Length));
                    UpdateState(() => operationProgress = new OperationProgress("WindowsUpdate", WingetProgressPhase.Starting, 0, 0, selected.Length));
                    var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                    {
                        UpdateState(() => operationProgress = update);
                        progress?.Report(update);
                        NotifyStateChanged();
                    });
                    var outcome = await windowsUpdateService.InstallAsync(selected, options, cancellationToken, forwardingProgress).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        var errorMsg = !string.IsNullOrWhiteSpace(outcome.Error?.Message)
                            ? outcome.Error.Message
                            : UserText("Error_WindowsInstallFailed", "Windows Update install failed.");
                        throw new InvalidOperationException(errorMsg);
                    }

                    UpdateState(() => lastWindowsUpdateResults.AddRange(outcome.Rows));
                    var selectedKeys = selected.Select(WindowsUpdateFingerprint).ToHashSet(StringComparer.Ordinal);
                    var resultKeys = outcome.Rows.Select(result => WindowsUpdateFingerprint(result.Identity)).ToArray();
                    if (resultKeys.Length != selectedKeys.Count ||
                        resultKeys.Distinct(StringComparer.Ordinal).Count() != selectedKeys.Count ||
                        resultKeys.Any(key => !selectedKeys.Contains(key)))
                    {
                        throw new InvalidOperationException(UserText("Error_WindowsIncompleteResult", "Windows Update did not return a result for every selected update and revision. Scan again before retrying."));
                    }
                    foreach (var result in outcome.Rows)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var logMessage = result.Succeeded
                            ? UserText("Activity_WindowsCompleted", "Completed.")
                            : (string.IsNullOrWhiteSpace(result.Message) ? $"Result Code: {result.ResultCode}" : $"{result.Message} (Result Code: {result.ResultCode})");
                        AddActivity(ActivityCategory.WindowsUpdate, severity, result.Title, logMessage);
                        Logger?.Invoke(
                            result.Succeeded ? AppLogLevel.Verbose : AppLogLevel.Error,
                            $"[Windows Update Result] Title: {result.Title}, Succeeded: {result.Succeeded}, ResultCode: {result.ResultCode}, Message: {result.Message}",
                            nameof(InstallWindowsUpdatesAsync));
                    }

                    var failedUpdates = outcome.Rows.Where(result => !result.Succeeded).ToArray();
                    if (failedUpdates.Length > 0)
                    {
                        var failedTitles = string.Join(", ", failedUpdates.Select(f => f.Title));
                        throw new InvalidOperationException($"One or more Windows updates failed: {failedTitles}");
                    }

                    UpdateState(() => operationProgress = operationProgress! with { Phase = WingetProgressPhase.Completed, Percentage = 100, PackagePercentage = 100, CompletedPackages = selected.Length });
                    progress?.Report(ReadState(() => operationProgress!));

                    if (outcome.Rows.Any(result => result.RebootRequired))
                    {
                        AddActivity(ActivityCategory.WindowsUpdate, ActivitySeverity.Information, UserText("Activity_RestartRequired", "Restart required"), UserText("Activity_WindowsRestartAfterInstall", "One or more Windows updates require a restart."));
                    }
                },
                "Unable to install Windows updates.")
            .ConfigureAwait(false);
    }

    internal static string CleanVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return string.Empty;

        int firstDigitIndex = -1;
        for (int i = 0; i < version.Length; i++)
        {
            if (char.IsDigit(version[i]))
            {
                firstDigitIndex = i;
                break;
            }
        }

        if (firstDigitIndex >= 0)
        {
            return version[firstDigitIndex..].Trim();
        }

        return version.Trim();
    }

    internal static bool IsUpToDate(string? installed, string? available)
    {
        if (string.IsNullOrWhiteSpace(installed)) return false;
        if (string.IsNullOrWhiteSpace(available)) return false;

        installed = CleanVersion(installed);
        available = CleanVersion(available);

        if (Version.TryParse(installed, out var installedVer) && Version.TryParse(available, out var availableVer))
        {
            return installedVer >= availableVer;
        }

        // WinGet owns the ordering of versions outside System.Version's numeric format.
        return false;
    }
}
