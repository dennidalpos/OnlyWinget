// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;

namespace OnlyWinget.Models;

public sealed class PackageInterrogationResult
{
    public bool Success { get; init; }
    public bool IsReducedMode { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Source { get; init; } = AppEntry.DefaultSource;
    public string InstallerType { get; init; } = string.Empty;
    public string ManifestFingerprint { get; init; } = string.Empty;
    public DateTime InterrogatedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ResolvedInstallerOption> InstallerOptions { get; init; } = Array.Empty<ResolvedInstallerOption>();
    public IReadOnlyList<string> AvailableScopes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableArchitectures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableLocales { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableInstallerTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableInstallModes { get; init; } = Array.Empty<string>();
    public SelectedInstallOptions DefaultSelection { get; init; } = new();
}
