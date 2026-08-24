using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
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

        var isPwshAvailable = await IsCommandAvailableAsync(
                "pwsh.exe",
                ["-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"],
                cancellationToken)
            .ConfigureAwait(false);

        var isLegacyPsAvailable = await IsCommandAvailableAsync(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "$PSVersionTable.PSVersion.ToString()"],
                cancellationToken)
            .ConfigureAwait(false);

        var isPowerShellAvailable = isPwshAvailable || isLegacyPsAvailable;
        var preferredPs = isPwshAvailable ? "pwsh.exe" : "powershell.exe";
        PowerShellExecutableProvider.SetPreferredExecutable(preferredPs);

        var psType = isPwshAvailable
            ? "PowerShell 7 (pwsh)"
            : isLegacyPsAvailable ? "Windows PowerShell 5.1" : null;

        var windowsUpdate = isSupportedOs && isPowerShellAvailable
            ? await CheckWindowsUpdateComAsync(preferredPs, cancellationToken).ConfigureAwait(false)
            : new WindowsUpdateCapability(false, null);

        var buildNumber = OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : (int?)null;
        var (edition, displayVersion) = ReadWindowsEditionInfo();
        var systemLanguage = CultureInfo.CurrentUICulture.Name;
        var isElevated = CheckIsElevated();

        return new SystemCapabilities(
            isSupportedOs,
            isWingetAvailable,
            isPowerShellAvailable,
            windowsUpdate.IsAvailable,
            windowsUpdate.UnavailableReason,
            wingetVersion,
            buildNumber,
            edition,
            displayVersion,
            systemLanguage,
            isElevated,
            psType,
            isPwshAvailable,
            isLegacyPsAvailable);
    }

    private async Task<WindowsUpdateCapability> CheckWindowsUpdateComAsync(string psExecutable, CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                psExecutable,
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
        try
        {
            var result = await commandRunner.RunAsync(command, arguments, cancellationToken).ConfigureAwait(false);
            return result.Succeeded && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    private static (string? edition, string? displayVersion) ReadWindowsEditionInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return (null, null);
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is not null)
            {
                var productName = key.GetValue("ProductName")?.ToString();
                var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString();
                var editionId = key.GetValue("EditionID")?.ToString();

                if (Environment.OSVersion.Version.Build >= 22000 && productName is not null && productName.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                {
                    productName = "Windows 11" + productName["Windows 10".Length..];
                }

                return (productName ?? editionId, displayVersion);
            }
        }
        catch
        {
            // Ignore registry read errors
        }

        return (RuntimeInformation.OSDescription, null);
    }

    private static bool CheckIsElevated()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

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

    private sealed record WindowsUpdateCapability(bool IsAvailable, string? UnavailableReason);
}
