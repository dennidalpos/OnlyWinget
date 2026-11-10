namespace OnlyWinget.Application.Winget;

public sealed record WingetOperationOutcome<T>(
    bool Succeeded,
    IReadOnlyList<T> Rows,
    ClassifiedWingetError? Error,
    string RawOutput)
{
    public static WingetOperationOutcome<T> Success(IReadOnlyList<T> rows, string rawOutput) =>
        new(true, rows, null, rawOutput);

    public static WingetOperationOutcome<T> Failure(ClassifiedWingetError error, string rawOutput) =>
        new(false, [], error, rawOutput);
}
