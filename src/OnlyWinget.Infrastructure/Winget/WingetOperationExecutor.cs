using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetOperationExecutor(
    IWingetCommandRunner commandRunner,
    WingetCommandBuilder commandBuilder,
    WingetErrorClassifier errorClassifier) : IOperationExecutor
{
    public async Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var results = new List<OperationExecutionResult>();
        for (var index = 0; index < plan.Selections.Count; index++)
        {
            var selection = plan.Selections[index];
            cancellationToken.ThrowIfCancellationRequested();

            var lastReportedPercentage = -1;
            var lastReportedPhase = WingetProgressPhase.Starting;
            var commandProgress = new InlineProgress<WingetProgress>(update =>
            {
                var packagePercentage = update.Percentage ?? 0;
                var aggregate = (int)Math.Clamp(
                    Math.Round(((index + (packagePercentage / 100d)) / plan.Selections.Count) * 100d),
                    0,
                    100);
                if (aggregate == lastReportedPercentage && update.Phase == lastReportedPhase)
                {
                    return;
                }

                lastReportedPercentage = aggregate;
                lastReportedPhase = update.Phase;
                progress?.Report(new OperationProgress(
                    selection.Package.Id,
                    update.Phase,
                    aggregate,
                    index,
                    plan.Selections.Count));
            });

            var commandResult = await commandRunner.RunAsync(
                    "winget",
                    commandBuilder.Build(selection),
                    cancellationToken,
                    commandProgress)
                .ConfigureAwait(false);

            results.Add(new OperationExecutionResult(
                selection,
                commandResult,
                errorClassifier.Classify(commandResult)));
        }

        return new OperationExecutionSummary(results);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
