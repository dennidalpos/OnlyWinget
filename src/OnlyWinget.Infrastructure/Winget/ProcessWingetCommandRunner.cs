using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class ProcessWingetCommandRunner(
    IExternalProcessRunner processRunner,
    WingetProgressParser progressParser,
    ILogger<ProcessWingetCommandRunner>? logger = null) : IWingetCommandRunner
{
    public ProcessWingetCommandRunner()
        : this(new global::OnlyWinget.Infrastructure.System.ProcessExternalProcessRunner(), new WingetProgressParser(), null)
    {
    }

    public async Task<WingetCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<WingetProgress>? progress = null,
        TimeSpan? timeout = null,
        bool requireElevation = false)
    {
        progressParser.Reset();
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

        var mustElevate = requireElevation || IsWriteOperation(command, arguments);

        logger?.LogInformation("Running winget command '{Command}' (Elevated: {Elevated}) with args: {Arguments}", command, mustElevate, string.Join(" ", arguments));
        var result = await processRunner.RunAsync(command, arguments, cancellationToken, lineProgress, timeout, requireElevation: mustElevate)
            .ConfigureAwait(false);
        logger?.LogDebug("Command '{Command}' finished with exit code {ExitCode}", command, result.ExitCode);

        if (!result.Succeeded &&
            !(command == "winget" && arguments.Count > 1 && arguments[0] == "source" && arguments[1] == "reset") &&
            (result.StandardOutput.Contains("0x8a15005e") ||
             result.StandardError.Contains("0x8a15005e") ||
             result.StandardOutput.Contains("The server certificate did not match", global::System.StringComparison.OrdinalIgnoreCase) ||
             result.StandardError.Contains("The server certificate did not match", global::System.StringComparison.OrdinalIgnoreCase)))
        {
            var resetResult = await processRunner.RunAsync("winget", ["source", "reset", "--force"], cancellationToken, timeout: timeout, requireElevation: true)
                .ConfigureAwait(false);
            if (resetResult.Succeeded)
            {
                result = await processRunner.RunAsync(command, arguments, cancellationToken, lineProgress, timeout, requireElevation: mustElevate)
                    .ConfigureAwait(false);
            }
        }

        progress?.Report(new WingetProgress(
            result.Succeeded ? WingetProgressPhase.Completed : WingetProgressPhase.Failed,
            result.Succeeded ? 100 : null,
            null));
        return new WingetCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static bool IsWriteOperation(string command, IReadOnlyList<string> arguments)
    {
        if (string.Equals(command, "winget", StringComparison.OrdinalIgnoreCase) && arguments.Count > 0)
        {
            var action = arguments[0].ToLowerInvariant();
            return action is "install" or "uninstall" or "upgrade" or "pin" ||
                   (action == "source" && arguments.Count > 1 && arguments[1].ToLowerInvariant() is "add" or "remove" or "reset");
        }
        return false;
    }
}
