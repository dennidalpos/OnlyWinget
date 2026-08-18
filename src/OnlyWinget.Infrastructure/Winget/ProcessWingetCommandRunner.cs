using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class ProcessWingetCommandRunner(
    IExternalProcessRunner processRunner,
    WingetProgressParser progressParser,
    ILogger<ProcessWingetCommandRunner>? logger = null) : IWingetCommandRunner
{
    // --disable-interactivity was introduced in winget (App Installer) v1.4. Older versions reject it
    // as an unrecognized argument, so it is stripped when an older version is detected.
    private static readonly Version MinimumDisableInteractivityVersion = new(1, 4);

    private readonly SemaphoreSlim versionCheckLock = new(1, 1);
    private Version? cachedWingetVersion;
    private bool wingetVersionChecked;

    public ProcessWingetCommandRunner()
        : this(new global::OnlyWinget.Infrastructure.System.ProcessExternalProcessRunner(), new WingetProgressParser(), null)
    {
    }

    public async Task<WingetCommandResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<WingetProgress>? progress = null,
        TimeSpan? timeout = null)
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

        if (command == "winget" && arguments.Contains("--disable-interactivity"))
        {
            var wingetVersion = await GetWingetVersionAsync(cancellationToken).ConfigureAwait(false);
            if (wingetVersion is not null && wingetVersion < MinimumDisableInteractivityVersion)
            {
                arguments = arguments.Where(argument => argument != "--disable-interactivity").ToArray();
            }
        }

        logger?.LogInformation("Running winget command '{Command}' with args: {Arguments}", command, string.Join(" ", arguments));
        var result = await processRunner.RunAsync(command, arguments, cancellationToken, lineProgress, timeout)
            .ConfigureAwait(false);
        logger?.LogDebug("Command '{Command}' finished with exit code {ExitCode}", command, result.ExitCode);

        if (!result.Succeeded &&
            !(command == "winget" && arguments.Count > 1 && arguments[0] == "source" && arguments[1] == "reset") &&
            (result.StandardOutput.Contains("0x8a15005e") ||
             result.StandardError.Contains("0x8a15005e") ||
             result.StandardOutput.Contains("The server certificate did not match", global::System.StringComparison.OrdinalIgnoreCase) ||
             result.StandardError.Contains("The server certificate did not match", global::System.StringComparison.OrdinalIgnoreCase)))
        {
            var resetResult = await processRunner.RunAsync("winget", ["source", "reset", "--force"], cancellationToken, timeout: timeout)
                .ConfigureAwait(false);
            if (resetResult.Succeeded)
            {
                result = await processRunner.RunAsync(command, arguments, cancellationToken, lineProgress, timeout)
                    .ConfigureAwait(false);
            }
        }

        progress?.Report(new WingetProgress(
            result.Succeeded ? WingetProgressPhase.Completed : WingetProgressPhase.Failed,
            result.Succeeded ? 100 : null,
            null));
        return new WingetCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private async Task<Version?> GetWingetVersionAsync(CancellationToken cancellationToken)
    {
        if (wingetVersionChecked)
        {
            return cachedWingetVersion;
        }

        await versionCheckLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (wingetVersionChecked)
            {
                return cachedWingetVersion;
            }

            var result = await processRunner.RunAsync("winget", ["--version"], cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                var versionText = result.StandardOutput.Trim().TrimStart('v').Split('-')[0];
                cachedWingetVersion = Version.TryParse(versionText, out var parsed) ? parsed : null;
            }

            wingetVersionChecked = true;
            return cachedWingetVersion;
        }
        finally
        {
            versionCheckLock.Release();
        }
    }
}
