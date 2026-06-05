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
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Source { get; set; } = AppEntry.DefaultSource;
    public string Scope { get; set; } = string.Empty;
    public string InstallMode { get; set; } = InstallModes.SilentWithProgress;
    public string Architecture { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string InstallerType { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public bool SupportsInstallLocation { get; set; } = true;
    public bool SupportsLog { get; set; } = true;
    public string AdditionalCustomArgs { get; set; } = string.Empty;
    public string OverrideArgs { get; set; } = string.Empty;
    public bool? AdvancedArgumentsReviewed { get; set; }
    public string ElevationRequirement { get; set; } = string.Empty;
}

public sealed class PresetFileRoot
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("presetName")]
    public string PresetName { get; set; } = string.Empty;

    [JsonPropertyName("apps")]
    public List<PresetAppItem> Apps { get; set; } = new();
}

public sealed class PresetAppItem
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = AppEntry.DefaultSource;

    [JsonPropertyName("action")]
    public string Action { get; set; } = AppActions.Install;
}
