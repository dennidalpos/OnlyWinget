// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class SelectedInstallOptions
{
    public string Scope { get; set; } = string.Empty;
    public string InstallMode { get; set; } = InstallModes.SilentWithProgress;
    public string Architecture { get; set; } = string.Empty;
    public IReadOnlyList<string> SelectedArchitectures { get; set; } = Array.Empty<string>();
    public string Locale { get; set; } = string.Empty;
    public string InstallerType { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public string AdditionalCustomArgs { get; set; } = string.Empty;
    public string OverrideArgs { get; set; } = string.Empty;
    public string ElevationRequirement { get; set; } = string.Empty;
}
