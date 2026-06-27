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
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
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

        return new ExternalProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static async Task<string> ReadOutputAsync(
        StreamReader reader,
        IProgress<string>? outputLines,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var currentLine = new StringBuilder();
        var buffer = new char[256];
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
        catch (InvalidOperationException)
        {
        }
    }
}
