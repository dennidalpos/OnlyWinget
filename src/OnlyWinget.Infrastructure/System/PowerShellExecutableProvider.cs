using System.Runtime.Versioning;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

/// <summary>
/// Discovers and provides the active PowerShell executable (pwsh.exe for PowerShell 7+ or powershell.exe for Windows PowerShell 5.1).
/// </summary>
public static class PowerShellExecutableProvider
{
    private static string? preferredExecutable;
    private static readonly Lock InitLock = new();

    public static string GetPreferredExecutable()
    {
        if (preferredExecutable is not null)
        {
            return preferredExecutable;
        }

        lock (InitLock)
        {
            if (preferredExecutable is not null)
            {
                return preferredExecutable;
            }

            preferredExecutable = "powershell.exe";
            return preferredExecutable;
        }
    }

    public static void SetPreferredExecutable(string executable)
    {
        lock (InitLock)
        {
            preferredExecutable = string.IsNullOrWhiteSpace(executable) ? "powershell.exe" : executable;
        }
    }
}
