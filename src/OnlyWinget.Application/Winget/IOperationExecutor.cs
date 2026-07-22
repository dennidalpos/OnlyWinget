using OnlyWinget.Domain.Operations;

namespace OnlyWinget.Application.Winget;

public interface IOperationExecutor
{
    Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null,
        bool continueAfterFailure = false,
        int maxRetries = 0);
}
