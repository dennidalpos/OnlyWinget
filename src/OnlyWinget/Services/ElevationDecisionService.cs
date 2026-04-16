// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Models;

namespace OnlyWinget.Services;

/// <summary>
/// Determines the appropriate <see cref="ElevationMode"/> for a winget install operation
/// based on the current process state, the requested scope, and the manifest's
/// ElevationRequirement field.
/// </summary>
public static class ElevationDecisionService
{
    // Canonical manifest ElevationRequirement values (case-insensitive comparison used throughout)
    private const string ElevationRequired = "elevationRequired";
    private const string ElevatesSelf = "elevatesSelf";
    private const string ElevationProhibited = "elevationProhibited";

    /// <summary>
    /// Decides how to launch winget for an install operation.
    /// </summary>
    /// <param name="isCurrentProcessElevated">Whether the calling process is already running as admin.</param>
    /// <param name="scope">The requested install scope ("machine", "user", or empty).</param>
    /// <param name="manifestElevationRequirement">The ElevationRequirement string from the installer manifest node (may be empty).</param>
    public static ElevationMode Decide(bool isCurrentProcessElevated, string scope, string manifestElevationRequirement)
    {
        var requirement = (manifestElevationRequirement ?? string.Empty).Trim();

        // Manifest explicitly prohibits elevation — never launch elevated.
        if (string.Equals(requirement, ElevationProhibited, System.StringComparison.OrdinalIgnoreCase))
        {
            return ElevationMode.ElevationProhibited;
        }

        // Installer self-elevates — don't force elevation from the outside.
        if (string.Equals(requirement, ElevatesSelf, System.StringComparison.OrdinalIgnoreCase))
        {
            return ElevationMode.SelfElevatingPossible;
        }

        // Manifest requires elevation.
        if (string.Equals(requirement, ElevationRequired, System.StringComparison.OrdinalIgnoreCase))
        {
            return isCurrentProcessElevated ? ElevationMode.Normal : ElevationMode.ElevatedRequired;
        }

        // Machine-scope install without current elevation needs elevation.
        var isMachineScope = string.Equals(scope?.Trim(), "machine", System.StringComparison.OrdinalIgnoreCase);
        if (isMachineScope && !isCurrentProcessElevated)
        {
            return ElevationMode.ElevatedRequired;
        }

        // Machine-scope and already elevated: just run normally.
        if (isMachineScope)
        {
            return ElevationMode.Normal;
        }

        // No specific requirement — run normally.
        return ElevationMode.Normal;
    }
}
