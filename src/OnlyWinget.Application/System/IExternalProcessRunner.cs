namespace OnlyWinget.Application.System;

public interface IExternalProcessRunner
{
    Task<ExternalProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines = null,
        TimeSpan? timeout = null,
        bool requireElevation = false);
}

public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
