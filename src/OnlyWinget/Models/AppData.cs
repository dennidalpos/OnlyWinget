// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OnlyWinget.Models;

public sealed class AppDataRoot
{
    public List<AppTabData> Tabs { get; set; } = new();
}

public sealed class AppTabData
{
    public string Name { get; set; } = string.Empty;
    public List<AppDataItem> Apps { get; set; } = new();
}

public sealed class AppDataItem
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Source { get; set; } = "winget";
    public string Version { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string InstallMode { get; set; } = InstallModes.SilentWithProgress;
    public string Architecture { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string InstallerType { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public string AdditionalCustomArgs { get; set; } = string.Empty;
    public string OverrideArgs { get; set; } = string.Empty;
    public string ManifestFingerprint { get; set; } = string.Empty;
    public string InterrogatedAtUtc { get; set; } = string.Empty;
    public string ElevationRequirement { get; set; } = string.Empty;
}

public sealed class PresetFileRoot
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("presetName")]
    public string PresetName { get; set; } = string.Empty;

    [JsonPropertyName("apps")]
    public List<AppDataItem> Apps { get; set; } = new();
}
