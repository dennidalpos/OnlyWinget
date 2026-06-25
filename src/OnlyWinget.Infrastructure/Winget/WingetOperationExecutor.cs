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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var results = new List<OperationExecutionResult>();
        foreach (var selection in plan.Selections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var commandResult = await commandRunner.RunAsync(
                    "winget",
                    commandBuilder.Build(selection),
                    cancellationToken)
                .ConfigureAwait(false);

            results.Add(new OperationExecutionResult(
                selection,
                commandResult,
                errorClassifier.Classify(commandResult)));
        }

        return new OperationExecutionSummary(results);
    }
}
