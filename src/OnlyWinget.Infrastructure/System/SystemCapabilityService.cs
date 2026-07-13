using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class SystemCapabilityService(IExternalProcessRunner commandRunner) : ISystemCapabilityService
{
    private const int MinimumSupportedBuild = 17763;

    public async Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var isSupportedOs = OperatingSystem.IsWindows() &&
            Environment.OSVersion.Version.Build >= MinimumSupportedBuild;

        var wingetVersion = (string?)null;
        var isWingetAvailable = false;
        try
        {
            var wingetResult = await commandRunner.RunAsync("winget", ["--version"], cancellationToken).ConfigureAwait(false);
            if (wingetResult.Succeeded && !string.IsNullOrWhiteSpace(wingetResult.StandardOutput))
            {
                isWingetAvailable = true;
                wingetVersion = wingetResult.StandardOutput.Trim();
            }
        }
        catch
        {
            // Leave winget as unavailable
        }

        var isPowerShellAvailable = await IsCommandAvailableAsync(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "$PSVersionTable.PSVersion.ToString()"],
                cancellationToken)
            .ConfigureAwait(false);

        var windowsUpdate = isSupportedOs && isPowerShellAvailable
            ? await CheckWindowsUpdateComAsync(cancellationToken).ConfigureAwait(false)
            : new WindowsUpdateCapability(false, null);

        var buildNumber = OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : (int?)null;

        return new SystemCapabilities(
            isSupportedOs,
            isWingetAvailable,
            isPowerShellAvailable,
            windowsUpdate.IsAvailable,
            windowsUpdate.UnavailableReason,
            wingetVersion,
            buildNumber);
    }

    private async Task<WindowsUpdateCapability> CheckWindowsUpdateComAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                "powershell.exe",
                [
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    "try { $null = New-Object -ComObject Microsoft.Update.Session; 'available'; exit 0 } catch { $_.Exception.Message; exit 1 }"
                ],
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            return new WindowsUpdateCapability(true, null);
        }

        var reason = string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError.Trim()
            : result.StandardOutput.Trim();
        return new WindowsUpdateCapability(false, string.IsNullOrWhiteSpace(reason) ? null : reason);
    }

    private async Task<bool> IsCommandAvailableAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(command, arguments, cancellationToken).ConfigureAwait(false);
        return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private sealed record WindowsUpdateCapability(bool IsAvailable, string? UnavailableReason);
}
