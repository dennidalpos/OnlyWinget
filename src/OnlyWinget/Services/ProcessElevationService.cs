// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Security.Principal;

namespace OnlyWinget.Services;

/// <summary>
/// Provides information about the elevation state of the current process.
/// </summary>
public static class ProcessElevationService
{
    private static readonly bool _isElevated = CheckIsElevated();

    /// <summary>
    /// Returns true when the current process is running with administrator rights.
    /// </summary>
    public static bool IsRunningAsAdministrator => _isElevated;

    private static bool CheckIsElevated()
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
}
