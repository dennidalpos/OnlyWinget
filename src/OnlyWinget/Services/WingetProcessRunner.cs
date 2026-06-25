// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OnlyWinget.Services;

public sealed class WingetProcessRunner : IWingetCommandRunner
{
    private const int WideConsoleWidth = 500;
    private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromHours(4);
    private readonly WingetRuntimeEnvironment _runtimeEnvironment;
    private readonly WingetOutputClassifier _outputClassifier;
    private readonly TimeSpan? _processTimeoutOverride;

    public WingetProcessRunner(
        WingetRuntimeEnvironment runtimeEnvironment,
        WingetOutputClassifier outputClassifier,
        TimeSpan? processTimeout = null)
    {
        _runtimeEnvironment = runtimeEnvironment ?? throw new ArgumentNullException(nameof(runtimeEnvironment));
        _outputClassifier = outputClassifier ?? throw new ArgumentNullException(nameof(outputClassifier));
        _processTimeoutOverride = processTimeout;
        if (_processTimeoutOverride.HasValue && _processTimeoutOverride.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(processTimeout), "Process timeout must be greater than zero.");
        }
    }

    public WingetCommandResult Run(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        return RunAsync(singleArg, args, onOutputLine, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<WingetCommandResult> RunAsync(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var runtimeDirectory = _runtimeEnvironment.EnsureLocalRuntimeDirectory();
        var commandArgs = BuildCommandArgs(singleArg, args);
        var processStartInfo = CreateProcessStartInfo(runtimeDirectory, commandArgs);
        var processTimeout = GetProcessTimeout(commandArgs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(processTimeout);

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return new WingetCommandResult { ExitCode = 9999, Output = _outputClassifier.GetErrorMessage(9999) };
        }

        var output = new List<string>();
        var error = new List<string>();
        var outputTask = ReadStreamAsync(process.StandardOutput, output, onOutputLine, timeoutCts.Token);
        var errorTask = ReadStreamAsync(process.StandardError, error, onOutputLine, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            return new WingetCommandResult
            {
                ExitCode = 9997,
                Output = "event=winget_process_cancelled reason=cancellation_requested"
            };
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return new WingetCommandResult
            {
                ExitCode = 9998,
                Output = $"event=winget_process_timeout timeout_seconds={(int)processTimeout.TotalSeconds}"
            };
        }

        var outputText = string.Join(Environment.NewLine, output);
        var errorText = string.Join(Environment.NewLine, error);
        var combined = string.IsNullOrEmpty(errorText)
            ? outputText
            : string.IsNullOrEmpty(outputText)
                ? errorText
                : outputText + Environment.NewLine + errorText;
        return new WingetCommandResult { ExitCode = process.ExitCode, Output = combined };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have exited between cancellation and cleanup.
        }
    }

    private static IReadOnlyList<string> BuildCommandArgs(string? singleArg, IReadOnlyList<string> args)
    {
        if (string.IsNullOrWhiteSpace(singleArg))
        {
            return args;
        }

        var commandArgs = new List<string>(capacity: args.Count + 1)
        {
            singleArg
        };
        commandArgs.AddRange(args);
        return commandArgs;
    }

    private TimeSpan GetProcessTimeout(IReadOnlyList<string> commandArgs)
    {
        if (_processTimeoutOverride.HasValue)
        {
            return _processTimeoutOverride.Value;
        }

        if (commandArgs.Count == 0)
        {
            return DefaultProcessTimeout;
        }

        return commandArgs[0].ToLowerInvariant() switch
        {
            "install" or "upgrade" or "uninstall" => TimeSpan.FromMinutes(90),
            "source" => TimeSpan.FromMinutes(5),
            "show" or "search" or "list" => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromMinutes(10)
        };
    }

    private static ProcessStartInfo CreateProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = ShouldUseWideConsole(commandArgs)
            ? CreateWideConsoleProcessStartInfo(runtimeDirectory, commandArgs)
            : CreateDirectWingetProcessStartInfo(runtimeDirectory, commandArgs);

        processStartInfo.Environment["TMP"] = runtimeDirectory;
        processStartInfo.Environment["TEMP"] = runtimeDirectory;
        return processStartInfo;
    }

    private static ProcessStartInfo CreateDirectWingetProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = CreateBaseProcessStartInfo("winget", runtimeDirectory);
        foreach (var arg in commandArgs)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        return processStartInfo;
    }

    private static ProcessStartInfo CreateWideConsoleProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = CreateBaseProcessStartInfo("powershell.exe", runtimeDirectory);
        processStartInfo.ArgumentList.Add("-NoLogo");
        processStartInfo.ArgumentList.Add("-NoProfile");
        processStartInfo.ArgumentList.Add("-NonInteractive");
        processStartInfo.ArgumentList.Add("-Command");
        processStartInfo.ArgumentList.Add(BuildWideConsoleWingetCommand(commandArgs));
        return processStartInfo;
    }

    private static ProcessStartInfo CreateBaseProcessStartInfo(string fileName, string runtimeDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = runtimeDirectory
        };
    }

    private static bool ShouldUseWideConsole(IReadOnlyList<string> commandArgs)
    {
        if (commandArgs.Count == 0)
        {
            return false;
        }

        var command = commandArgs[0];
        if (string.Equals(command, "search", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(command, "upgrade", StringComparison.OrdinalIgnoreCase) &&
            !commandArgs.Any(arg => string.Equals(arg, "--log", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildWideConsoleWingetCommand(IReadOnlyList<string> commandArgs)
    {
        var escapedArgs = string.Join(
            ", ",
            commandArgs.Select(static arg => $"'{EscapePowerShellLiteral(arg)}'"));

        return string.Join(
            "; ",
            "$rawUi = $Host.UI.RawUI",
            "if ($null -ne $rawUi) { try { $rawUi.BufferSize = New-Object Management.Automation.Host.Size(" + WideConsoleWidth + ", $rawUi.BufferSize.Height) } catch { } }",
            "& winget @(" + escapedArgs + ")",
            "exit $LASTEXITCODE");
    }

    private static string EscapePowerShellLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static async Task ReadStreamAsync(StreamReader reader, ICollection<string> target, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line == null)
            {
                break;
            }

            target.Add(line);
            onOutputLine?.Invoke(line);
        }
    }
}
