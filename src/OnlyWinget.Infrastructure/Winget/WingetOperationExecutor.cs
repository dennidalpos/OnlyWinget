using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetOperationExecutor(
    IWingetCommandRunner commandRunner,
    WingetCommandBuilder commandBuilder,
    WingetErrorClassifier errorClassifier,
    TimeSpan? retryDelay = null) : IOperationExecutor
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
        for (var index = 0; index < plan.Selections.Count; index++)
        {
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

            WingetCommandResult commandResult = default!;
            ClassifiedWingetError? classifiedError = null;
            var attemptCount = 0;
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
                catch (Exception exception)
                {
                    global::System.Diagnostics.Debug.WriteLine($"WingetOperationExecutor.ExecuteAsync: {exception}");
                    commandResult = new WingetCommandResult(-1, string.Empty, exception.Message);
                }

                classifiedError = errorClassifier.Classify(commandResult);
                var succeeded = commandResult.Succeeded || classifiedError?.Kind == WingetErrorKind.NoUpdates;
                if (succeeded || !errorClassifier.IsRetryable(classifiedError) || attempt >= maxAttempts)
                {
                    break;
                }

                var delay = retryDelay ?? TimeSpan.FromMilliseconds(1000);
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }

            results.Add(new OperationExecutionResult(
                selection,
                commandResult,
                classifiedError,
                attemptCount));
            if (!results[^1].Succeeded && !continueAfterFailure)
            {
                break;
            }
        }

        return new OperationExecutionSummary(results);
    }
}
