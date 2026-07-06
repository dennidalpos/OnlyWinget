using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record OperationExecutionResult(
    PackageSelection Selection,
    WingetCommandResult CommandResult,
    ClassifiedWingetError? Error)
{
    public bool Succeeded => CommandResult.Succeeded || Error?.Kind == WingetErrorKind.NoUpdates;
}
