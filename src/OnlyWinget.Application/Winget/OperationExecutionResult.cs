using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record OperationExecutionResult(
    PackageSelection Selection,
    WingetCommandResult CommandResult,
    ClassifiedWingetError? Error,
    int AttemptCount = 1)
{
    public bool Succeeded => CommandResult.Succeeded || Error?.Kind == WingetErrorKind.NoUpdates;
}
