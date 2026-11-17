using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetOperationExecutor(
    IWingetCommandRunner commandRunner,
    WingetCommandBuilder commandBuilder,
    WingetErrorClassifier errorClassifier,
    TimeSpan? retryDelay = null,
    ILogger<WingetOperationExecutor>? logger = null) : IOperationExecutor
{
    public async Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null,
        bool continueAfterFailure = false,
        int maxRetries = 0,
        bool bypassHashValidation = false)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var results = new List<OperationExecutionResult>();
        var attemptCount = 0;
        WingetCommandResult? commandResult = null;
        try
        {
            for (var index = 0; index < plan.Selections.Count; index++)
            {
                attemptCount = 0;
                commandResult = null;
                var selection = plan.Selections[index];
                cancellationToken.ThrowIfCancellationRequested();

                var lastReportedPackagePercentage = -1;
                var lastReportedPhase = WingetProgressPhase.Starting;
                var commandProgress = new InlineProgress<WingetProgress>(update =>
                {
                    var packagePercentage = update.Percentage ?? 0;
                    var aggregate = (int)Math.Clamp(
                        Math.Round(((index + (packagePercentage / 100d)) / plan.Selections.Count) * 100d),
                        0,
                        100);
                    if (packagePercentage == lastReportedPackagePercentage && update.Phase == lastReportedPhase)
                    {
                        return;
                    }

                    lastReportedPackagePercentage = packagePercentage;
                    lastReportedPhase = update.Phase;
                    progress?.Report(new OperationProgress(
                        selection.Package.Id,
                        update.Phase,
                        aggregate,
                        packagePercentage,
                        index,
                        plan.Selections.Count));
                });

                ClassifiedWingetError? classifiedError = null;
                var maxAttempts = Math.Max(1, 1 + maxRetries);

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    attemptCount = attempt;

                    if (attempt > 1)
                    {
                        progress?.Report(new OperationProgress(
                            selection.Package.Id,
                            WingetProgressPhase.Starting,
                            (int)Math.Clamp(Math.Round(((double)index / plan.Selections.Count) * 100d), 0, 100),
                            0,
                            index,
                            plan.Selections.Count));
                    }

                    var attemptStopwatch = global::System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        commandResult = await commandRunner.RunAsync(
                                "winget",
                                commandBuilder.Build(selection, bypassHashValidation),
                                cancellationToken,
                                commandProgress,
                                global::System.TimeSpan.FromMinutes(30))
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        commandResult = new WingetCommandResult(
                            -1,
                            string.Empty,
                            exception.Message,
                            exception is TimeoutException);
                    }

                    classifiedError = errorClassifier.Classify(commandResult);
                    if (classifiedError?.Kind == WingetErrorKind.NotFound
                        && commandBuilder.BuildByInstalledName(selection, bypassHashValidation) is { } byName)
                    {
                        logger?.LogInformation(
                            "winget {Action} {PackageId} not matched by id; retrying by installed name {Name}.",
                            selection.Action, selection.Package.Id, selection.InstalledName);
                        try
                        {
                            commandResult = await commandRunner.RunAsync(
                                    "winget", byName, cancellationToken, commandProgress, global::System.TimeSpan.FromMinutes(30))
                                .ConfigureAwait(false);
                            classifiedError = errorClassifier.Classify(commandResult);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception) when (exception is not OutOfMemoryException)
                        {
                            commandResult = new WingetCommandResult(-1, string.Empty, exception.Message, exception is TimeoutException);
                            classifiedError = errorClassifier.Classify(commandResult);
                        }
                    }

                    var succeeded = commandResult.Succeeded || classifiedError?.Kind.IsSuccessfulOutcome() == true;
                    var willRetry = !succeeded
                        && attempt < maxAttempts
                        && !commandResult.TimedOut
                        && errorClassifier.IsRetryable(commandResult, classifiedError);
                    LogAttempt(selection, attempt, maxAttempts, commandResult, classifiedError, succeeded, willRetry, attemptStopwatch.Elapsed);
                    if (!willRetry)
                    {
                        break;
                    }

                    var delay = retryDelay ?? WingetErrorClassifier.RetryDelayFor(classifiedError);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                }

                results.Add(new OperationExecutionResult(
                    selection,
                    commandResult!,
                    classifiedError,
                    attemptCount));
                if (!results[^1].Succeeded && !continueAfterFailure)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var completedCount = results.Count;
            for (var index = completedCount; index < plan.Selections.Count; index++)
            {
                var attempts = index == completedCount ? attemptCount : 0;
                var message = attempts == 0
                    ? "Operation cancelled before this package started."
                    : "Operation cancelled; package completion is unconfirmed. Check installed status before retrying.";
                var cancelledCommand = index == completedCount && commandResult is { Succeeded: false }
                    ? commandResult
                    : new WingetCommandResult(-1, string.Empty, string.Empty);
                results.Add(new OperationExecutionResult(plan.Selections[index], cancelledCommand,
                    new ClassifiedWingetError(WingetErrorKind.Cancelled, message), attempts));
            }
            throw new OperationExecutionCanceledException(results, cancellationToken, exception);
        }

        return new OperationExecutionSummary(results);
    }

    private void LogAttempt(
        PackageSelection selection,
        int attempt,
        int maxAttempts,
        WingetCommandResult result,
        ClassifiedWingetError? error,
        bool succeeded,
        bool willRetry,
        TimeSpan elapsed)
    {
        if (logger is null)
        {
            return;
        }

        var exitCode = $"0x{unchecked((uint)result.ExitCode):X8}";
        if (succeeded)
        {
            logger.LogInformation(
                "winget {Action} {PackageId} succeeded on attempt {Attempt}/{MaxAttempts} in {Elapsed:F1}s (exit {ExitCode}, kind {Kind}).",
                selection.Action, selection.Package.Id, attempt, maxAttempts, elapsed.TotalSeconds, exitCode, error?.Kind.ToString() ?? "None");
            return;
        }

        logger.LogWarning(
            "winget {Action} {PackageId} failed on attempt {Attempt}/{MaxAttempts} in {Elapsed:F1}s: exit {ExitCode}, kind {Kind}, timedOut {TimedOut}, willRetry {WillRetry}. stderr: {StdErr} | stdout: {StdOut}",
            selection.Action, selection.Package.Id, attempt, maxAttempts, elapsed.TotalSeconds, exitCode,
            error?.Kind.ToString() ?? "Unknown", result.TimedOut, willRetry,
            Truncate(result.StandardError), Truncate(result.StandardOutput));
    }

    private static string Truncate(string text)
    {
        var singleLine = string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Any(char.IsLetter)));
        return singleLine.Length <= 600 ? singleLine : singleLine[..600] + "...";
    }
}
