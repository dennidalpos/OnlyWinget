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
using OnlyWinget.Models;

namespace OnlyWinget.Services;

/// <summary>
/// Launches winget with elevated privileges via the Windows shell "runas" verb.
/// Because elevated processes run in a separate session, stdout/stderr cannot
/// be redirected directly. Instead a log file is always written and the launcher
/// returns its path so the caller can read diagnostics after completion.
/// </summary>
public sealed class ElevatedWingetLauncher : IElevatedWingetLauncher
{
    private const int StructuredLogValueMaxLength = 500;
    private static readonly TimeSpan DefaultLaunchTimeout = TimeSpan.FromMinutes(90);
    private static readonly TimeSpan LogPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Launches winget elevated with the supplied arguments and waits for it to exit.
    /// Returns the process exit code and a summary line. When a log path is available,
    /// new log lines are reported while the elevated process is running.
    /// </summary>
    public WingetCommandResult Launch(
        IReadOnlyList<string> args,
        string? logFilePath,
        Action<string>? onOutputLine = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultLaunchTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Elevated launch timeout must be greater than zero.");
        }

        // Ensure the --log argument is present so we can capture diagnostics.
        var effectiveArgs = EnsureLogArgument(args, logFilePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "winget",
            Arguments = BuildArgumentString(effectiveArgs),
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = false
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new WingetCommandResult
                {
                    ExitCode = 9999,
                    Output = "event=elevated_launch_failed reason=process_start_returned_null"
                };
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(effectiveTimeout);
            var logTailTask = StartLogTail(logFilePath, onOutputLine, timeoutCts.Token);

            try
            {
                process.WaitForExitAsync(timeoutCts.Token).GetAwaiter().GetResult();
                StopLogTail(logTailTask, timeoutCts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryKillProcessTree(process);
                StopLogTail(logTailTask, timeoutCts);
                return new WingetCommandResult
                {
                    ExitCode = 9997,
                    Output = "event=elevated_launch_cancelled reason=cancellation_requested"
                };
            }
            catch (OperationCanceledException)
            {
                TryKillProcessTree(process);
                StopLogTail(logTailTask, timeoutCts);
                return new WingetCommandResult
                {
                    ExitCode = 9998,
                    Output = $"event=elevated_launch_timeout timeout_seconds={(int)effectiveTimeout.TotalSeconds}"
                };
            }

            var exitCode = process.ExitCode;
            var logNote = !string.IsNullOrWhiteSpace(logFilePath)
                ? $" log={Quote(logFilePath)}"
                : string.Empty;
            return new WingetCommandResult
            {
                ExitCode = exitCode,
                Output = $"event=elevated_launch_completed exit_code={exitCode}{logNote}"
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception win32 && win32.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED — user declined the UAC prompt.
            return new WingetCommandResult
            {
                ExitCode = 1223,
                Output = "event=elevated_launch_cancelled reason=uac_declined"
            };
        }
        catch (Exception ex)
        {
            return new WingetCommandResult
            {
                ExitCode = 9999,
                Output = $"event=elevated_launch_failed reason={Quote(ex.Message)}"
            };
        }
    }

    private static Task? StartLogTail(string? logFilePath, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logFilePath) || onOutputLine == null)
        {
            return null;
        }

        return Task.Run(() => TailLogFileAsync(logFilePath, onOutputLine, cancellationToken), CancellationToken.None);
    }

    private static void StopLogTail(Task? logTailTask, CancellationTokenSource timeoutCts)
    {
        if (logTailTask == null)
        {
            return;
        }

        if (!timeoutCts.IsCancellationRequested)
        {
            timeoutCts.Cancel();
        }

        try
        {
            logTailTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task TailLogFileAsync(string logFilePath, Action<string> onOutputLine, CancellationToken cancellationToken)
    {
        var position = 0L;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                position = ReadNewLogLines(logFilePath, position, onOutputLine);
                await Task.Delay(LogPollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        ReadNewLogLines(logFilePath, position, onOutputLine);
    }

    internal static long ReadNewLogLines(string logFilePath, long position, Action<string> onOutputLine)
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                return position;
            }

            using var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (position > stream.Length)
            {
                position = 0;
            }

            stream.Seek(position, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            while (reader.ReadLine() is { } line)
            {
                onOutputLine(line);
            }

            return stream.Position;
        }
        catch (IOException)
        {
            return position;
        }
        catch (UnauthorizedAccessException)
        {
            return position;
        }
    }

    private static void TryKillProcessTree(Process process)
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
            // Elevated child processes may have already exited or deny termination.
        }
    }

    private static IReadOnlyList<string> EnsureLogArgument(IReadOnlyList<string> args, string? logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return args;
        }

        // If --log is already present do not add it again.
        if (args.Any(a => string.Equals(a, "--log", StringComparison.OrdinalIgnoreCase)))
        {
            return args;
        }

        var extended = new List<string>(args) { "--log", logFilePath };
        return extended;
    }

    /// <summary>
    /// Builds a single argument string suitable for <see cref="ProcessStartInfo.Arguments"/>.
    /// Each token is individually quoted if it contains spaces.
    /// </summary>
    internal static string BuildArgumentString(IReadOnlyList<string> args)
    {
        return string.Join(" ", args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string arg)
    {
        if (arg.Length > 0 && !arg.Any(static c => char.IsWhiteSpace(c) || c == '"'))
        {
            return arg;
        }

        var sb = new StringBuilder();
        sb.Append('"');
        var backslashes = 0;

        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
                continue;
            }

            sb.Append('\\', backslashes);
            backslashes = 0;
            sb.Append(c);
        }

        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    private static string Quote(string value) => $"\"{SanitizeStructuredLogValue(value)}\"";

    private static string SanitizeStructuredLogValue(string value)
    {
        var builder = new StringBuilder(Math.Min(value.Length, StructuredLogValueMaxLength));
        foreach (var character in value)
        {
            if (builder.Length >= StructuredLogValueMaxLength)
            {
                break;
            }

            builder.Append(character switch
            {
                '"' => '\'',
                _ when char.IsControl(character) => ' ',
                _ => character
            });
        }

        return builder.ToString().Trim();
    }
}
