// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;

namespace OnlyWinget.Models;

public sealed class ResolvedInstallerOption
{
    public string Architecture { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string InstallerType { get; init; } = string.Empty;
    public bool SupportsSilent { get; init; }
    public bool SupportsSilentWithProgress { get; init; }
    public bool SupportsInteractive { get; init; } = true;
    public string DisplayLabel { get; init; } = string.Empty;

    // Capability flags derived from manifest
    /// <summary>
    /// Raw ElevationRequirement field from the manifest (e.g. "elevationRequired", "elevatesSelf", "elevationProhibited").
    /// Empty string means unspecified.
    /// </summary>
    public string ElevationRequirement { get; init; } = string.Empty;

    /// <summary>
    /// Arguments declared as unsupported by this installer node (e.g. "Location", "Log").
    /// Fields with unsupported arguments should be disabled in the UI.
    /// </summary>
    public IReadOnlyList<string> UnsupportedArguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True when the installer node does NOT list "Location" as an unsupported argument.
    /// </summary>
    public bool SupportsLocation => !ContainsIgnoreCase(UnsupportedArguments, "Location");

    /// <summary>
    /// True when the installer node does NOT list "Log" as an unsupported argument.
    /// </summary>
    public bool SupportsLog => !ContainsIgnoreCase(UnsupportedArguments, "Log");

    private static bool ContainsIgnoreCase(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i], value, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
