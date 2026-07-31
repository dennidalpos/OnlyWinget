using OnlyWinget.Application.Activity;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> RefreshUpdatesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.RefreshingUpdates,
                async () =>
                {
                    RequireWinget();
                    var enabledSources = GetEnabledSourceNames();
                    if (enabledSources.Count == 0)
                    {
                        throw new InvalidOperationException("Enable at least one winget source before refreshing updates.");
                    }

                    updates.Clear();
                    lastOperationResults.Clear();
                    var sourceErrors = new List<string>();
                    var loadTasks = enabledSources.Select(async source =>
                    {
                        var outcome = await updateLoader.LoadUpdatesAsync(source, cancellationToken).ConfigureAwait(false);
                        if (!outcome.Succeeded && outcome.Error?.Kind != WingetErrorKind.NoUpdates)
                        {
                            lock (sourceErrors)
                            {
                                sourceErrors.Add($"{source}: {outcome.Error?.Message ?? "winget upgrade failed."}");
                            }
                        }
                        else
                        {
                            lock (updates)
                            {
                                updates.AddRange(outcome.Rows);
                            }
                        }
                    }).ToArray();
                    await Task.WhenAll(loadTasks).ConfigureAwait(false);

                    if (updates.Count == 0 && sourceErrors.Count > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    var distinctUpdates = updates
                        .DistinctBy(update => update.Package)
                        .OrderBy(update => update.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    updates.Clear();
                    updates.AddRange(distinctUpdates);

                    await RefreshPackageMetadataAsync(
                            updates.Select(update => update.Package),
                            cancellationToken)
                        .ConfigureAwait(false);

                    updateSelection.ReplaceAvailable(updates.Select(update => update.Package));
                    AddActivity(ActivitySeverity.Information, "Updates refreshed", $"{updates.Count} update(s).");

                    if (sourceErrors.Count > 0)
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
        var selections = updateSelection.Selected
            .Select(package => new PackageSelection(package, PackageAction.Upgrade))
            .ToArray();
        return await ExecutePlanAsync(new OperationPlan("Selected updates", selections), cancellationToken, progress)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ScanWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ScanningWindowsUpdates,
                async () =>
                {
                    RequireWindowsUpdate();
                    lastWindowsUpdateResults.Clear();
                    var outcome = await windowsUpdateService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update scan failed.");
                    }

                    windowsUpdates.Clear();
                    windowsUpdates.AddRange(outcome.Rows
                        .DistinctBy(update => WindowsUpdateFingerprint(update.Identity))
                        .OrderBy(update => update.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(update => update.Identity.UpdateId, StringComparer.OrdinalIgnoreCase));
                    windowsUpdateSelection.ReplaceAvailable(windowsUpdates.Select(update => update.Identity));
                    AddActivity(ActivitySeverity.Information, "Windows Update scan completed", $"{windowsUpdates.Count} update(s).");
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
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var selected = windowsUpdateSelection.Selected.ToArray();
        return await RunAsync(
                ApplicationBusyState.InstallingWindowsUpdates,
                async () =>
                {
                    RequireWindowsUpdate();
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one Windows update before installing.");
                    }

                    lastWindowsUpdateResults.Clear();
                    AddActivity(ActivitySeverity.Information, "Windows Update install started", $"{selected.Length} update(s).");
                    operationProgress = new OperationProgress("WindowsUpdate", WingetProgressPhase.Starting, 0, 0, selected.Length);
                    var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                    {
                        operationProgress = update;
                        progress?.Report(update);
                        NotifyStateChanged();
                    });
                    var outcome = await windowsUpdateService.InstallAsync(selected, options, cancellationToken, forwardingProgress).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update install failed.");
                    }

                    lastWindowsUpdateResults.AddRange(outcome.Rows);
                    foreach (var result in outcome.Rows)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var logMessage = result.Succeeded
                            ? "Completed."
                            : (string.IsNullOrWhiteSpace(result.Message) ? $"Result Code: {result.ResultCode}" : $"{result.Message} (Result Code: {result.ResultCode})");
                        AddActivity(severity, result.Title, logMessage);
                        Logger?.Invoke(
                            AppLogLevel.Verbose,
                            $"[Windows Update Result] Title: {result.Title}, Succeeded: {result.Succeeded}, ResultCode: {result.ResultCode}, Message: {result.Message}",
                            nameof(InstallSelectedWindowsUpdatesAsync));
                    }

                    if (outcome.Rows.Any(result => !result.Succeeded))
                    {
                        throw new InvalidOperationException("One or more Windows updates failed.");
                    }

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
        if (string.IsNullOrWhiteSpace(available)) return true;

        installed = CleanVersion(installed);
        available = CleanVersion(available);

        if (string.Equals(installed, available, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Version.TryParse(installed, out var installedVer) && Version.TryParse(available, out var availableVer))
        {
            return installedVer >= availableVer;
        }

        var installedParts = installed.Split('.', '-', '+', '_');
        var availableParts = available.Split('.', '-', '+', '_');

        for (int i = 0; i < Math.Min(installedParts.Length, availableParts.Length); i++)
        {
            var instPart = installedParts[i];
            var availPart = availableParts[i];

            if (int.TryParse(instPart, out var instInt) && int.TryParse(availPart, out var availInt))
            {
                if (instInt != availInt)
                {
                    return instInt > availInt;
                }
            }
            else
            {
                var cmp = string.Compare(instPart, availPart, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp > 0;
                }
            }
        }

        return installedParts.Length >= availableParts.Length;
    }
}
