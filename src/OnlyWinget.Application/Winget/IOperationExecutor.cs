using OnlyWinget.Domain.Operations;

namespace OnlyWinget.Application.Winget;

public interface IOperationExecutor
{
    /// <exception cref="OperationExecutionCanceledException">Preserves completed and cancelled package results.</exception>
    Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null,
        bool continueAfterFailure = false,
        int maxRetries = 0,
        bool bypassHashValidation = false);
}
