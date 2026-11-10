namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateOperationOutcome<T>(
    bool Succeeded,
    IReadOnlyList<T> Rows,
    string RawOutput,
    WindowsUpdateError? Error)
{
    public static WindowsUpdateOperationOutcome<T> Success(IReadOnlyList<T> rows, string rawOutput) =>
        new(true, rows, rawOutput, null);

    public static WindowsUpdateOperationOutcome<T> Failure(WindowsUpdateError error, string rawOutput) =>
        new(false, [], rawOutput, error);
}
