// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

/// <summary>
/// Describes how a winget operation should be launched with respect to elevation.
/// </summary>
public enum ElevationMode
{
    /// <summary>Launch normally without elevation.</summary>
    Normal,

    /// <summary>Elevation is mandatory (manifest requires it or machine scope was requested without current admin rights).</summary>
    ElevatedRequired,

    /// <summary>Elevation is preferred but not strictly required.</summary>
    ElevatedPreferred,

    /// <summary>Manifest explicitly prohibits elevation; must not run elevated.</summary>
    ElevationProhibited,

    /// <summary>Installer may self-elevate; caller should not force elevation.</summary>
    SelfElevatingPossible
}
