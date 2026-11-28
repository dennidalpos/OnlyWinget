using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

/// <summary>Restart control through the system shutdown.exe (no elevation prompt is added; it needs the user's own rights).</summary>
public sealed class ShutdownRestartService(ILogger<ShutdownRestartService>? logger = null) : ISystemRestartService
{
    public bool IsRestartPending()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return IsRestartPendingWindows();
    }

    [SupportedOSPlatform("windows")]
    private static bool IsRestartPendingWindows()
    {
        // The same markers Windows Update and component servicing write when a restart is required.
        string[] keys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"
        ];
        foreach (var key in keys)
        {
            try
            {
                using var subKey = Registry.LocalMachine.OpenSubKey(key);
                if (subKey is not null)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or global::System.Security.SecurityException or IOException)
            {
                // Unreadable marker: treat as absent rather than claiming a restart is needed.
            }
        }

        return false;
    }

    public Task<bool> ScheduleRestartAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var seconds = (int)Math.Clamp(delay.TotalSeconds, 0, 3600);
        return RunShutdownAsync(
            ["/r", "/t", seconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture), "/c", "OnlyWinget: restarting to finish Windows updates"],
            cancellationToken);
    }

    public Task<bool> CancelRestartAsync(CancellationToken cancellationToken) => RunShutdownAsync(["/a"], cancellationToken);

    private async Task<bool> RunShutdownAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("shutdown.exe {Arguments} exited with {ExitCode}.", string.Join(' ', arguments), process.ExitCode);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is global::System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger?.LogWarning(ex, "shutdown.exe could not be started.");
            return false;
        }
    }
}
