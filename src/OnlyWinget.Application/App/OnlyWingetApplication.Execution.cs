using OnlyWinget.Application.Activity;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> ApplyActivePresetAsync(
        PackageAction action,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var active = RequireActivePreset();
        var includedPackages = active.Packages
            .Where(package => presetInstallSelection.Selected.Contains(package))
            .ToArray();
        var plan = operationPlanner.CreatePresetPlan(new Preset(active.Name, includedPackages), action);
        return await ExecutePlanAsync(plan, cancellationToken, progress).ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RetryFailedOperationsAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var failedSelections = lastOperationResults
            .Where(result => !result.Succeeded)
            .Select(result => result.Selection)
            .ToArray();

        if (failedSelections.Length == 0)
        {
            return ApplicationActionResult.Failure("No failed operations to retry.");
        }

        var retryPlan = new OperationPlan("Retry failed operations", failedSelections);
        return await ExecutePlanAsync(retryPlan, cancellationToken, progress).ConfigureAwait(false);
    }

    public ApplicationActionResult ClearActivity() =>
        Run(() =>
        {
            activity.Clear();
            userVisibleError = null;
        });

    public ApplicationActionResult RestoreActivity(IEnumerable<ActivityEntry> entries) =>
        Run(() =>
        {
            ArgumentNullException.ThrowIfNull(entries);
            activity.Clear();
            activity.AddRange(entries);
            userVisibleError = null;
        });

    public ApplicationActionResult ReportExternalFailure(string message) =>
        Run(() => throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(message) ? "External operation failed." : message.Trim()));

    private async Task<ApplicationActionResult> ExecutePlanAsync(
        OperationPlan plan,
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress)
    {
        return await RunAsync(
                ApplicationBusyState.ExecutingOperation,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    if (!plan.HasWork)
                    {
                        throw new InvalidOperationException("Select at least one package before applying an operation.");
                    }

                    var validatedSelections = new List<PackageSelection>();
                    var validationFailures = new List<OperationExecutionResult>();
                    var skippedResults = new List<OperationExecutionResult>();

                    foreach (var selection in plan.Selections)
                    {
                        try
                        {
                            var validated = await ValidatePackageAsync(selection.Package, cancellationToken).ConfigureAwait(false);

                            // Preventative check
                            if (selection.Action is PackageAction.Install or PackageAction.Upgrade)
                            {
                                var installedStatus = await packageResolver.CheckInstalledStatusAsync(validated.Package, cancellationToken).ConfigureAwait(false);
                                if (installedStatus.IsInstalled)
                                {
                                    bool skip = false;
                                    string skipMessage = string.Empty;

                                    if (selection.Action == PackageAction.Install)
                                    {
                                        skip = true;
                                        skipMessage = $"Package is already present (Installed: {installedStatus.InstalledVersion}).";
                                    }
                                    else if (selection.Action == PackageAction.Upgrade)
                                    {
                                        if (IsUpToDate(installedStatus.InstalledVersion, validated.Version))
                                        {
                                            skip = true;
                                            skipMessage = $"Package is already updated (Installed: {installedStatus.InstalledVersion}, Available: {validated.Version}).";
                                        }
                                    }

                                    if (skip)
                                    {
                                        var resultRow = new WingetCommandResult(0, skipMessage, string.Empty);
                                        var executionResult = new OperationExecutionResult(
                                            new PackageSelection(validated.Package, selection.Action),
                                            resultRow,
                                            null);
                                        skippedResults.Add(executionResult);
                                        continue;
                                    }
                                }
                            }

                            validatedSelections.Add(new PackageSelection(validated.Package, selection.Action));
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                        {
                            if (!ContinueOperationsAfterFailure)
                            {
                                throw;
                            }
                            var error = new ClassifiedWingetError(WingetErrorKind.Unknown, exception.Message);
                            var dummyResult = new WingetCommandResult(-1, string.Empty, exception.Message);
                            validationFailures.Add(new OperationExecutionResult(selection, dummyResult, error));
                        }
                    }

                    var validatedPlan = new OperationPlan(plan.Name, validatedSelections);
                    lastOperationResults.Clear();
                    lastOperationResults.AddRange(validationFailures);
                    lastOperationResults.AddRange(skippedResults);

                    foreach (var result in validationFailures)
                    {
                        AddActivity(ActivitySeverity.Error, result.Selection.Package.Id, result.Error?.Message ?? "Validation failed.");
                    }

                    foreach (var result in skippedResults)
                    {
                        AddActivity(ActivitySeverity.Success, result.Selection.Package.Id, CreateOperationActivityMessage(result));
                    }

                    if (validatedSelections.Count > 0)
                    {
                        AddActivity(ActivitySeverity.Information, "Operation started", plan.Name);
                        operationProgress = new OperationProgress(string.Empty, WingetProgressPhase.Starting, 0, 0, 0, plan.Selections.Count);
                        var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                        {
                            operationProgress = update;
                            progress?.Report(update);
                            NotifyStateChanged();
                        });
                        var summary = await operationExecutor.ExecuteAsync(
                            validatedPlan,
                            cancellationToken,
                            forwardingProgress,
                            ContinueOperationsAfterFailure,
                            MaxPackageOperationRetries,
                            BypassHashValidation).ConfigureAwait(false);

                        lastOperationResults.AddRange(summary.Results);

                        foreach (var result in summary.Results)
                        {
                            var severity = result.Error?.Kind == WingetErrorKind.NoUpdates
                                ? ActivitySeverity.Warning
                                : (result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error);
                            var message = CreateOperationActivityMessage(result);
                            AddActivity(severity, result.Selection.Package.Id, string.IsNullOrWhiteSpace(message) ? "Completed." : message);
                            Logger?.Invoke(
                                result.Succeeded ? AppLogLevel.Verbose : AppLogLevel.Error,
                                $"[Package Result] ID: {result.Selection.Package.Id}, Action: {result.Selection.Action}, Succeeded: {result.Succeeded}, ExitCode: {result.CommandResult.ExitCode}, StdOut: {result.CommandResult.StandardOutput.Trim()}, StdErr: {result.CommandResult.StandardError.Trim()}, AttemptCount: {result.AttemptCount}",
                                nameof(ApplySelectedUpdatesAsync));
                        }

                        var succeededPackages = summary.Results
                            .Concat(skippedResults)
                            .Where(result => result.Succeeded)
                            .Select(result => result.Selection.Package)
                            .ToArray();
                        updates.RemoveAll(update => succeededPackages.Contains(update.Package));
                        updateSelection.ReplaceAvailable(updates.Select(update => update.Package));

                        if (!summary.Succeeded || validationFailures.Count > 0)
                        {
                            throw new InvalidOperationException("One or more winget operations failed.");
                        }

                        operationProgress = operationProgress with { Phase = WingetProgressPhase.Completed, Percentage = 100, PackagePercentage = 100, CompletedPackages = plan.Selections.Count };
                        progress?.Report(operationProgress);
                    }
                    else
                    {
                        var succeededPackages = skippedResults
                            .Where(result => result.Succeeded)
                            .Select(result => result.Selection.Package)
                            .ToArray();
                        updates.RemoveAll(update => succeededPackages.Contains(update.Package));
                        updateSelection.ReplaceAvailable(updates.Select(update => update.Package));

                        if (validationFailures.Count > 0)
                        {
                            throw new InvalidOperationException("One or more winget operations failed.");
                        }
                    }
                },
                "Unable to complete the operation.")
            .ConfigureAwait(false);
    }

    private static string CreateOperationActivityMessage(OperationExecutionResult result)
    {
        var exitCode = result.CommandResult.ExitCode;
        var exitCodeSuffix = exitCode != 0
            ? $" (Exit code: {exitCode} / 0x{exitCode:X8})"
            : string.Empty;
        var attemptSuffix = result.AttemptCount > 1
            ? $" (Attempts: {result.AttemptCount})"
            : string.Empty;

        if (result.Error is not null)
        {
            var baseMsg = string.IsNullOrWhiteSpace(result.CommandResult.StandardError)
                ? result.Error.Message
                : $"{result.Error.Message} {result.CommandResult.StandardError.Trim()}";
            return baseMsg + exitCodeSuffix + attemptSuffix;
        }

        var output = result.CommandResult.StandardOutput.Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            return output + exitCodeSuffix + attemptSuffix;
        }

        var errorOutput = result.CommandResult.StandardError.Trim();
        var finalMsg = string.IsNullOrWhiteSpace(errorOutput) ? "Completed." : errorOutput;
        return finalMsg + exitCodeSuffix + attemptSuffix;
    }

    private void AddActivity(ActivitySeverity severity, string title, string message)
    {
        lock (stateLock)
        {
            activity.Add(new ActivityEntry(clock.GetUtcNow(), severity, title, message));
        }
        var logLevel = severity switch
        {
            ActivitySeverity.Error => AppLogLevel.Error,
            ActivitySeverity.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Information
        };
        Logger?.Invoke(logLevel, $"[Activity] {title}: {message}", nameof(AddActivity));
    }
}
