using Microsoft.Extensions.Logging;
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
        var plan = ReadState(() =>
        {
            var active = RequireActivePreset();
            var includedPackages = active.Packages
                .Where(package => presetInstallSelection.Selected.Contains(package))
                .ToArray();
            return operationPlanner.CreatePresetPlan(new Preset(active.Name, includedPackages), action);
        });
        return await ExecutePlanAsync(plan, cancellationToken, progress).ConfigureAwait(false);
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
                    var completedValidation = new HashSet<PackageSelection>();
                    UpdateState(lastOperationResults.Clear);

                    try
                    {
                        foreach (var selection in plan.Selections)
                        {
                            try
                            {
                                if (selection.Action == PackageAction.Uninstall)
                                {
                                    // WinGet validates one installed match; remote show is unnecessary.
                                    cancellationToken.ThrowIfCancellationRequested();
                                    validatedSelections.Add(selection);
                                    continue;
                                }

                                var validated = await ValidatePackageAsync(selection.Package, cancellationToken).ConfigureAwait(false);

                                // Skip packages whose installed version already satisfies the action.
                                if (selection.Action is PackageAction.Install or PackageAction.Upgrade)
                                {
                                    var installedStatus = await packageResolver.CheckInstalledStatusAsync(validated.Package, cancellationToken).ConfigureAwait(false);
                                    if (installedStatus.IsInstalled)
                                    {
                                        var skipMessage = selection.Action switch
                                        {
                                            PackageAction.Install => $"Package is already present (Installed: {installedStatus.InstalledVersion}).",
                                            PackageAction.Upgrade when IsUpToDate(installedStatus.InstalledVersion, validated.Version) =>
                                                $"Package is already updated (Installed: {installedStatus.InstalledVersion}, Available: {validated.Version}).",
                                            _ => null
                                        };

                                        if (skipMessage is not null)
                                        {
                                            var resultRow = new WingetCommandResult(0, skipMessage, string.Empty);
                                            var executionResult = new OperationExecutionResult(
                                                new PackageSelection(validated.Package, selection.Action),
                                                resultRow,
                                                null, AttemptCount: 0);
                                            completedValidation.Add(selection);
                                            RecordOperationResults([executionResult]);
                                            continue;
                                        }
                                    }
                                }

                                validatedSelections.Add(new PackageSelection(validated.Package, selection.Action));
                            }
                            catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                            {
                                var error = new ClassifiedWingetError(WingetErrorKind.Unknown, exception.Message);
                                var failedResult = new OperationExecutionResult(selection,
                                    new WingetCommandResult(-1, string.Empty, exception.Message), error, AttemptCount: 0);
                                validationFailures.Add(failedResult);
                                completedValidation.Add(selection);
                                RecordOperationResults([failedResult]);
                                if (!ContinueOperationsAfterFailure) throw;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        RecordOperationResults(plan.Selections.Where(selection => !completedValidation.Contains(selection))
                            .Select(selection => new OperationExecutionResult(selection,
                                new WingetCommandResult(-1, string.Empty, string.Empty),
                                new ClassifiedWingetError(WingetErrorKind.Cancelled, "Operation cancelled before this package started."),
                                AttemptCount: 0)).ToArray());
                        throw;
                    }

                    var validatedPlan = new OperationPlan(plan.Name, validatedSelections);
                    if (validatedSelections.Count > 0)
                    {
                        AddActivity(ActivitySeverity.Information, "Operation started", plan.Name);
                        UpdateState(() => operationProgress = new OperationProgress(string.Empty, WingetProgressPhase.Starting, 0, 0, 0, plan.Selections.Count));
                        var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                        {
                            UpdateState(() => operationProgress = update);
                            progress?.Report(update);
                            NotifyStateChanged();
                        });
                        OperationExecutionSummary summary;
                        var executionStartedAt = clock.GetUtcNow();
                        try
                        {
                            summary = await operationExecutor.ExecuteAsync(
                            validatedPlan,
                            cancellationToken,
                            forwardingProgress,
                            ContinueOperationsAfterFailure,
                            MaxPackageOperationRetries,
                                BypassHashValidation).ConfigureAwait(false);
                        }
                        catch (OperationExecutionCanceledException exception)
                        {
                            RecordOperationResults(exception.Summary.Results);
                            await InspectIncompleteOperationsAsync(exception.Summary.Results, executionStartedAt).ConfigureAwait(false);
                            throw;
                        }
                        RecordOperationResults(summary.Results);
                        await InspectIncompleteOperationsAsync(summary.Results, executionStartedAt).ConfigureAwait(false);

                        if (!summary.Succeeded || validationFailures.Count > 0)
                        {
                            var failedPackages = summary.Results
                                .Where(r => !r.Succeeded)
                                .Select(DescribeFailure)
                                .Concat(validationFailures.Select(f => $"{f.Selection.Package.Id} (validation)"))
                                .Distinct()
                                .ToArray();
                            var detail = failedPackages.Length > 0
                                ? $"One or more winget operations failed: {string.Join(", ", failedPackages)}"
                                : "One or more winget operations failed.";
                            throw new InvalidOperationException(detail);
                        }

                        UpdateState(() => operationProgress = operationProgress! with { Phase = WingetProgressPhase.Completed, Percentage = 100, PackagePercentage = 100, CompletedPackages = plan.Selections.Count });
                        progress?.Report(ReadState(() => operationProgress!));
                    }
                    else if (validationFailures.Count > 0)
                    {
                        throw new InvalidOperationException("One or more winget operations failed.");
                    }
                },
                "Unable to complete the operation.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// After a failed, killed or cancelled install/upgrade the real state is unknown (the installer may have
    /// half-applied). Report what winget now says, and remove only the failed run's download leftovers.
    /// Never uninstalls, purges or forces anything. Runs on its own short timeout so a cancel cannot hang it.
    /// </summary>
    private async Task InspectIncompleteOperationsAsync(
        IReadOnlyList<OperationExecutionResult> results,
        DateTimeOffset startedAt)
    {
        var incomplete = results
            .Where(result => !result.Succeeded
                && result.Selection.Action is PackageAction.Install or PackageAction.Upgrade
                && result.AttemptCount > 0)
            .ToArray();
        if (incomplete.Length == 0)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var result in incomplete)
        {
            var package = result.Selection.Package;
            try
            {
                var status = await packageResolver.CheckInstalledStatusAsync(package, timeout.Token).ConfigureAwait(false);
                var state = status.IsInstalled
                    ? $"still installed (version {status.InstalledVersion}); the {result.Selection.Action.ToString().ToLowerInvariant()} did not complete."
                    : "not installed.";
                AddActivity(ActivitySeverity.Warning, package.Id, $"State after the failed operation: {state}");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                appLogger?.LogDebug("Installed-state check after failure skipped for {PackageId}: {Message}", package.Id, exception.Message);
            }
        }

        if (residueCleaner is null)
        {
            return;
        }

        try
        {
            var removed = await residueCleaner
                .CleanAsync(incomplete.Select(result => result.Selection.Package.Id).ToArray(), startedAt, timeout.Token)
                .ConfigureAwait(false);
            if (removed.Count > 0)
            {
                AddActivity(
                    ActivitySeverity.Information,
                    "Cleaned up leftovers",
                    $"Removed temporary winget download folders left by the failed operation: {string.Join(", ", removed)}.");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            appLogger?.LogDebug("Residue cleanup skipped: {Message}", exception.Message);
        }
    }

    private static string DescribeFailure(OperationExecutionResult result)
    {
        var id = result.Selection.Package.Id;
        if (result.Error is not { } error)
        {
            return id;
        }

        var code = error.ExitCodeHex ?? $"0x{unchecked((uint)result.CommandResult.ExitCode):X8}";
        return error.Kind == WingetErrorKind.Cancelled ? id : $"{id} ({code} {error.Kind})";
    }

    private void LogToApplicationLog(AppLogLevel level, string message)
    {
        if (appLogger is null)
        {
            return;
        }

        var logLevel = level switch
        {
            AppLogLevel.Error => LogLevel.Error,
            AppLogLevel.Warning => LogLevel.Warning,
            AppLogLevel.Information => LogLevel.Information,
            _ => LogLevel.Debug
        };
        appLogger.Log(logLevel, "{Message}", message);
    }

    private static string SingleLine(string text, int maxLength = 400)
    {
        var line = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Any(char.IsLetter)));
        return line.Length <= maxLength ? line : line[..maxLength] + "...";
    }

    private void RecordOperationResults(IReadOnlyList<OperationExecutionResult> results)
    {
        UpdateState(() =>
        {
            lastOperationResults.AddRange(results);
            var succeededPackages = results.Where(result => result.Succeeded)
                .Select(result => result.Selection.Package).ToHashSet();
            updates.RemoveAll(update => succeededPackages.Contains(update.Package));
            updateSelection.ReplaceAvailable(updates.Select(update => update.Package));
        });
        foreach (var result in results)
        {
            var severity = result.Error?.Kind is WingetErrorKind.NoUpdates or WingetErrorKind.AlreadyInstalled
                or WingetErrorKind.RebootRequired
                ? ActivitySeverity.Warning
                : result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
            AddActivity(severity, result.Selection.Package.Id, CreateOperationActivityMessage(result));
            var packageResultLine =
                $"[Package Result] ID: {result.Selection.Package.Id}, Action: {result.Selection.Action}, Succeeded: {result.Succeeded}, " +
                $"ExitCode: 0x{unchecked((uint)result.CommandResult.ExitCode):X8}, Kind: {result.Error?.Kind.ToString() ?? "None"}, " +
                $"Attempts: {result.AttemptCount}, StdErr: {SingleLine(result.CommandResult.StandardError)}, StdOut: {SingleLine(result.CommandResult.StandardOutput)}";
            Logger?.Invoke(result.Succeeded ? AppLogLevel.Verbose : AppLogLevel.Error, packageResultLine, nameof(ExecutePlanAsync));
            LogToApplicationLog(result.Succeeded ? AppLogLevel.Information : AppLogLevel.Error, packageResultLine);
        }
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
        UpdateState(() =>
        {
            activity.Add(new ActivityEntry(clock.GetUtcNow(), severity, title, message));
        });
        var logLevel = severity switch
        {
            ActivitySeverity.Error => AppLogLevel.Error,
            ActivitySeverity.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Information
        };
        Logger?.Invoke(logLevel, $"[Activity] {title}: {message}", nameof(AddActivity));
        if (logLevel != AppLogLevel.Information)
        {
            // Success/info activity is already visible in the UI; warnings and errors are what a log reader needs.
            LogToApplicationLog(logLevel, $"[Activity] {title}: {SingleLine(message, 500)}");
        }
    }
}
