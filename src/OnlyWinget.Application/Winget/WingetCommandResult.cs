namespace OnlyWinget.Application.Winget;

public sealed record WingetCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut = false)
{
    public bool Succeeded => ExitCode == 0;
}
