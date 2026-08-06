using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class ProcessExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines = null,
        TimeSpan? timeout = null,
        bool requireElevation = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var actualTimeout = timeout ?? TimeSpan.FromSeconds(120);

        if (requireElevation && OperatingSystem.IsWindows() && !IsElevated())
        {
            return await RunElevatedAsync(command, arguments, cancellationToken, standardOutputLines, actualTimeout).ConfigureAwait(false);
        }

        using var timeoutCts = actualTimeout != Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource(actualTimeout)
            : null;
        using var linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;
        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        using var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            try
            {
                process.Start();
            }
            catch (Exception exception)
            {
                return new ExternalProcessResult(9009, string.Empty, exception.Message);
            }

            var standardOutput = ReadOutputAsync(process.StandardOutput, standardOutputLines, effectiveToken);
            var standardError = process.StandardError.ReadToEndAsync(effectiveToken);
            try
            {
                await process.WaitForExitAsync(effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (timeoutCts is not null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"Process execution timed out after {actualTimeout.TotalSeconds} seconds: {command}");
                }
                throw;
            }

            string stdout;
            string stderr;
            try
            {
                stdout = await standardOutput.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stdout = string.Empty;
                global::System.Diagnostics.Debug.WriteLine($"ProcessExternalProcessRunner.RunAsync (stdout): {ex}");
            }

            try
            {
                stderr = await standardError.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                global::System.Diagnostics.Debug.WriteLine($"ProcessExternalProcessRunner.RunAsync (stderr): {ex}");
            }

            return new ExternalProcessResult(process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"ProcessExternalProcessRunner.RunAsync (general): {exception}");
            return new ExternalProcessResult(-1, string.Empty, exception.Message);
        }
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static async Task<ExternalProcessResult> RunElevatedAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines,
        TimeSpan actualTimeout)
    {
        var runId = Guid.NewGuid().ToString("N");
        var tempFolder = Path.GetTempPath();
        var outPath = Path.Combine(tempFolder, $"onlywinget-elevated-out-{runId}.tmp");
        var errPath = Path.Combine(tempFolder, $"onlywinget-elevated-err-{runId}.tmp");
        var exitPath = Path.Combine(tempFolder, $"onlywinget-elevated-exit-{runId}.tmp");

        try
        {
            var formattedArgs = string.Join(" ", arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            var shellCmd = $"\"\"{command}\" {formattedArgs} > \"{outPath}\" 2> \"{errPath}\" & echo %ERRORLEVEL% > \"{exitPath}\"\"";

            using var process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c {shellCmd}";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Verb = "runas";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            try
            {
                process.Start();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED (user cancelled UAC prompt)
            {
                return new ExternalProcessResult(unchecked((int)0x8a150012), string.Empty, "Operation cancelled by user at UAC prompt.");
            }
            catch (Exception ex)
            {
                return new ExternalProcessResult(9009, string.Empty, ex.Message);
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var stdout = File.Exists(outPath) ? await File.ReadAllTextAsync(outPath, cancellationToken).ConfigureAwait(false) : string.Empty;
            var stderr = File.Exists(errPath) ? await File.ReadAllTextAsync(errPath, cancellationToken).ConfigureAwait(false) : string.Empty;

            if (!string.IsNullOrWhiteSpace(stdout) && standardOutputLines is not null)
            {
                foreach (var line in stdout.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
                {
                    standardOutputLines.Report(line);
                }
            }

            var exitCode = process.ExitCode;
            if (File.Exists(exitPath) && int.TryParse((await File.ReadAllTextAsync(exitPath, cancellationToken).ConfigureAwait(false)).Trim(), out var parsedCode))
            {
                exitCode = parsedCode;
            }

            return new ExternalProcessResult(exitCode, stdout, stderr);
        }
        finally
        {
            TryDeleteFile(outPath);
            TryDeleteFile(errPath);
            TryDeleteFile(exitPath);
        }
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore temporary cleanup errors
        }
    }

    private static async Task<string> ReadOutputAsync(
        StreamReader reader,
        IProgress<string>? outputLines,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var currentLine = new StringBuilder();
        var buffer = new char[256];
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                output.Append(buffer, 0, read);
                for (var index = 0; index < read; index++)
                {
                    if (buffer[index] is '\r' or '\n')
                    {
                        ReportLine(currentLine, outputLines);
                    }
                    else
                    {
                        currentLine.Append(buffer[index]);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"ProcessExternalProcessRunner.ReadOutputAsync: {exception}");
        }

        ReportLine(currentLine, outputLines);
        return output.ToString();
    }

    private static void ReportLine(StringBuilder line, IProgress<string>? outputLines)
    {
        if (line.Length > 0)
        {
            outputLines?.Report(line.ToString());
            line.Clear();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Win32Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"[ProcessRunner] Failed to kill process tree due to a Win32 error: {exception.Message} (Error code: {exception.NativeErrorCode})");
        }
        catch (InvalidOperationException exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"[ProcessRunner] Failed to kill process tree because process was invalid/already closed: {exception.Message}");
        }
        catch (Exception exception)
        {
            global::System.Diagnostics.Debug.WriteLine($"[ProcessRunner] Unexpected exception when killing process tree: {exception}");
        }
    }
}
