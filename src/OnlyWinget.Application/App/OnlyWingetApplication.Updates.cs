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
                    AddActivity(ActivitySeverity.Information, "Updates refreshed", $"{distinctUpdates.Length} update(s).");

                    if (sourceErrors.Length > 0)
                    {
                        AddActivity(
                            ActivitySeverity.Warning,
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

    public async Task<ApplicationActionResult> ApplySelectedUpdatesAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var selections = ReadState(() => updateSelection.Selected
            .Select(package => new PackageSelection(
                package,
                PackageAction.Upgrade,
                updates.FirstOrDefault(update => update.Package.Equals(package))?.Name))
            .ToArray());
        return await ExecutePlanAsync(new OperationPlan("Selected updates", selections), cancellationToken, progress)
            .ConfigureAwait(false);
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
            return Run(() => throw new InvalidOperationException("Select an update that needs reinstalling first."));
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
                AddActivity(ActivitySeverity.Warning, target.Package.Id, "Uninstall did not complete; the app was not reinstalled.");
                continue;
            }

            last = await ExecutePlanAsync(
                new OperationPlan($"Reinstall {target.Package.Id}: install",
                    [new PackageSelection(target.Package, PackageAction.Install)]),
                cancellationToken, progress).ConfigureAwait(false);
            if (!last.Succeeded)
            {
                AddActivity(ActivitySeverity.Error, target.Package.Id,
                    "The previous version was uninstalled but the new one failed to install. Install it again from Packages.");
            }
        }

        return last!;
    }

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
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update scan failed.");
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
                    AddActivity(ActivitySeverity.Information, "Windows Update scan completed", $"{ReadState(() => windowsUpdates.Count)} update(s).");
                },
                "Unable to scan Windows Update.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleWindowsUpdate(WindowsUpdateIdentity update) =>
        ToggleSelection(windowsUpdateSelection, update);

    public ApplicationActionResult ToggleAllWindowsUpdates() => Run(windowsUpdateSelection.ToggleAll);

    public ApplicationActionResult SetWindowsUpdatesSelection(IEnumerable<WindowsUpdateIdentity> updates, bool isSelected) =>
        Run(() => { foreach (var u in updates) windowsUpdateSelection.SetSelected(u, isSelected); });

    public async Task<ApplicationActionResult> InstallSelectedWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var selected = ReadState(() => windowsUpdateSelection.Selected.ToArray());
        return await RunAsync(
                ApplicationBusyState.InstallingWindowsUpdates,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWindowsUpdate();
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one Windows update before installing.");
                    }

                    UpdateState(lastWindowsUpdateResults.Clear);
                    AddActivity(ActivitySeverity.Information, "Windows Update install started", $"{selected.Length} update(s).");
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
                            : "Windows Update install failed.";
                        throw new InvalidOperationException(errorMsg);
                    }

                    UpdateState(() => lastWindowsUpdateResults.AddRange(outcome.Rows));
                    var selectedKeys = selected.Select(WindowsUpdateFingerprint).ToHashSet(StringComparer.Ordinal);
                    var resultKeys = outcome.Rows.Select(result => WindowsUpdateFingerprint(result.Identity)).ToArray();
                    if (resultKeys.Length != selectedKeys.Count ||
                        resultKeys.Distinct(StringComparer.Ordinal).Count() != selectedKeys.Count ||
                        resultKeys.Any(key => !selectedKeys.Contains(key)))
                    {
                        throw new InvalidOperationException("Windows Update did not return a result for every selected update and revision. Scan again before retrying.");
                    }
                    foreach (var result in outcome.Rows)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var logMessage = result.Succeeded
                            ? "Completed."
                            : (string.IsNullOrWhiteSpace(result.Message) ? $"Result Code: {result.ResultCode}" : $"{result.Message} (Result Code: {result.ResultCode})");
                        AddActivity(severity, result.Title, logMessage);
                        Logger?.Invoke(
                            result.Succeeded ? AppLogLevel.Verbose : AppLogLevel.Error,
                            $"[Windows Update Result] Title: {result.Title}, Succeeded: {result.Succeeded}, ResultCode: {result.ResultCode}, Message: {result.Message}",
                            nameof(InstallSelectedWindowsUpdatesAsync));
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
                        AddActivity(ActivitySeverity.Information, "Restart required", "One or more Windows updates require a restart.");
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
