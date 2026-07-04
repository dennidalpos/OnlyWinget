using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class ProcessExternalProcessRunner : IExternalProcessRunner
{
    public async Task<ExternalProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IProgress<string>? standardOutputLines = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

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

            var standardOutput = ReadOutputAsync(process.StandardOutput, standardOutputLines, cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
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
        catch (Exception)
        {
        }
    }
}
