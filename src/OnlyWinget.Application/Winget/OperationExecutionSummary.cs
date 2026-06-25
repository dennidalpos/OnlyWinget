namespace OnlyWinget.Application.Winget;

public sealed record OperationExecutionSummary(IReadOnlyList<OperationExecutionResult> Results)
{
    public bool Succeeded => Results.All(result => result.Succeeded);
}
