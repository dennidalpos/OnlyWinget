using OnlyWinget.Domain.Operations;

namespace OnlyWinget.Application.Winget;

public interface IOperationExecutor
{
    Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken);
}
