using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class ProcessExternalProcessRunner : IExternalProcessRunner
{
    public Task<ExternalProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines = null,
        TimeSpan? timeout = null,
        IProgress<string>? standardErrorLines = null) =>
        RunCoreAsync(command, arguments, cancellationToken, standardOutputLines, timeout, standardErrorLines, null);

    internal Task<ExternalProcessResult> RunWithInputAsync(string command, IReadOnlyList<string> arguments,
        string input, IProgress<string>? output, CancellationToken token) =>
        RunCoreAsync(command, arguments, token, output, Timeout.InfiniteTimeSpan, null, input);

    private static async Task<ExternalProcessResult> RunCoreAsync(
        string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines, TimeSpan? timeout, IProgress<string>? standardErrorLines,
        string? input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var actualTimeout = timeout ?? TimeSpan.FromSeconds(120);

        using var timeoutCts = actualTimeout != Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource(actualTimeout)
            : null;
        using var linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;
        var effectiveToken = linkedCts?.Token ?? cancellationToken;

        effectiveToken.ThrowIfCancellationRequested();
        using var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardInput = input is not null;
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
            var standardError = ReadOutputAsync(process.StandardError, standardErrorLines, effectiveToken);
            var standardInput = input is null ? Task.CompletedTask :
                WriteInputAsync(process.StandardInput.BaseStream, input, effectiveToken);
            try
            {
                await Task.WhenAll(process.WaitForExitAsync(effectiveToken), standardInput).WaitAsync(effectiveToken).ConfigureAwait(false);
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

    private static async Task WriteInputAsync(Stream stream, string input, CancellationToken token)
    {
        await using var ownedStream = stream;
        await ownedStream.WriteAsync(Encoding.UTF8.GetBytes(input), token).ConfigureAwait(false);
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
