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
using OnlyWinget.Models;

namespace OnlyWinget.Services;

/// <summary>
/// Launches winget with elevated privileges via the Windows shell "runas" verb.
/// Because elevated processes run in a separate session, stdout/stderr cannot
/// be redirected directly. Instead a log file is always written and the launcher
/// returns its path so the caller can read diagnostics after completion.
/// </summary>
public sealed class ElevatedWingetLauncher
{
    /// <summary>
    /// Launches winget elevated with the supplied arguments and waits for it to exit.
    /// Returns the process exit code and a summary line. Live output is not available
    /// for elevated launches; use <paramref name="logFilePath"/> for diagnostics.
    /// </summary>
    public WingetCommandResult Launch(IReadOnlyList<string> args, string? logFilePath)
    {
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

            process.WaitForExit();
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

    private static string Quote(string value) => $"\"{value.Replace("\"", "'")}\"";
}
