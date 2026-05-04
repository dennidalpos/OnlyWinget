// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class WingetPackageDetails
{
    public string Scope { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string InstallerType { get; init; } = string.Empty;

    public bool HasAnyInstallHint =>
        !string.IsNullOrWhiteSpace(Scope)
        || !string.IsNullOrWhiteSpace(Architecture)
        || !string.IsNullOrWhiteSpace(Locale)
        || !string.IsNullOrWhiteSpace(InstallerType);
}
