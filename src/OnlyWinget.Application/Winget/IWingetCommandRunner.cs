namespace OnlyWinget.Application.Winget;

public interface IWingetCommandRunner
{
    Task<WingetCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<WingetProgress>? progress = null,
        TimeSpan? timeout = null);
}
