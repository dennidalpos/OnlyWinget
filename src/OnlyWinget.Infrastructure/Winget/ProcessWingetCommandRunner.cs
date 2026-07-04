using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class ProcessWingetCommandRunner(
    IExternalProcessRunner processRunner,
    WingetProgressParser progressParser) : IWingetCommandRunner
{
    public ProcessWingetCommandRunner()
        : this(new global::OnlyWinget.Infrastructure.System.ProcessExternalProcessRunner(), new WingetProgressParser())
    {
    }

    public async Task<WingetCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<WingetProgress>? progress = null)
    {
        progress?.Report(new WingetProgress(WingetProgressPhase.Starting, 0, null));
        var lineProgress = progress is null
            ? null
            : new InlineProgress<string>(line =>
            {
                if (progressParser.Parse(line) is { } parsed)
                {
                    progress.Report(parsed);
                }
            });
        var result = await processRunner.RunAsync(command, arguments, cancellationToken, lineProgress)
            .ConfigureAwait(false);
        progress?.Report(new WingetProgress(
            result.Succeeded ? WingetProgressPhase.Completed : WingetProgressPhase.Failed,
            result.Succeeded ? 100 : null,
            null));
        return new WingetCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
