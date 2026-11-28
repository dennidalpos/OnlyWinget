using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record OperationExecutionResult(
    PackageSelection Selection,
    WingetCommandResult CommandResult,
    ClassifiedWingetError? Error,
    int AttemptCount = 1)
{
    public bool Succeeded => CommandResult.Succeeded || Error?.Kind.IsSuccessfulOutcome() == true;

    /// <summary>True when the installer finished but Windows must restart to complete it.</summary>
    public bool RebootRequired => Error?.Kind == WingetErrorKind.RebootRequired;
}
