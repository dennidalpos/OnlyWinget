namespace OnlyWinget.Application.Winget;

public sealed class OperationExecutionCanceledException : OperationCanceledException
{
    public OperationExecutionCanceledException(
        IReadOnlyList<OperationExecutionResult> results,
        CancellationToken cancellationToken,
        OperationCanceledException innerException)
        : base("Package operation cancelled.", innerException, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);
        Summary = new OperationExecutionSummary(Array.AsReadOnly(results.ToArray()));
    }

    public OperationExecutionSummary Summary { get; }
}
