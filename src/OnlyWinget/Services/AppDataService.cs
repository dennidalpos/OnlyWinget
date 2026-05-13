// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public enum AppDataLoadStatus
{
    Success,
    FileNotFound,
    InvalidData,
    IoError
}

public sealed class AppDataLoadResult
{
    public AppDataLoadStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public Dictionary<string, List<AppEntry>> Tabs { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> TabNames { get; init; } = new();
}

public sealed class SaveResult
{
    public bool Success { get; init; }
    public string Path { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class PresetImportResult
{
    public bool Success { get; init; }
    public string Path { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string ImportedPresetName { get; init; } = string.Empty;
    public List<AppEntry> Apps { get; init; } = new();
}

public sealed class AppDataService
{
    private readonly string _appDataRoot;

    public AppDataService(string? appDataRoot = null)
    {
        _appDataRoot = appDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnlyWinget");
    }

    public string GetJsonPath()
    {
        return Path.Combine(_appDataRoot, "AppsList.json");
    }

    public AppDataLoadResult Load(string jsonPath)
    {
        var tabs = new Dictionary<string, List<AppEntry>>(StringComparer.OrdinalIgnoreCase);
        var tabNames = new List<string>();

        if (!File.Exists(jsonPath))
        {
            CreateDefaultTab(tabs, tabNames);
            return CreateLoadResult(AppDataLoadStatus.FileNotFound, jsonPath, tabs, tabNames);
        }

        try
        {
            var rawText = File.ReadAllText(jsonPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return CreateFallbackLoadResult(
                    AppDataLoadStatus.InvalidData,
                    jsonPath,
                    "Il file dati è vuoto.");
            }

            var trimmed = rawText.TrimStart();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return CreateFallbackLoadResult(
                    AppDataLoadStatus.InvalidData,
                    jsonPath,
                    "Il formato dati legacy non e' piu' supportato.");
            }

            LoadTabbedFormat(rawText, tabs, tabNames);
        }
        catch (JsonException ex)
        {
            return CreateFallbackLoadResult(AppDataLoadStatus.InvalidData, jsonPath, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return CreateFallbackLoadResult(AppDataLoadStatus.InvalidData, jsonPath, ex.Message);
        }
        catch (IOException ex)
        {
            return CreateFallbackLoadResult(AppDataLoadStatus.IoError, jsonPath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateFallbackLoadResult(AppDataLoadStatus.IoError, jsonPath, ex.Message);
        }

        if (tabs.Count == 0)
        {
            return CreateFallbackLoadResult(
                AppDataLoadStatus.InvalidData,
                jsonPath,
                "Nessuna scheda valida trovata nel file dati.");
        }

        return CreateLoadResult(AppDataLoadStatus.Success, jsonPath, tabs, tabNames);
    }

    public SaveResult Save(string jsonPath, IReadOnlyList<string> tabNames, Dictionary<string, List<AppEntry>> tabs)
    {
        try
        {
            var root = new AppDataRoot();
            var usedTabNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawTabName in tabNames)
            {
                var safeTabName = NormalizeTabName(rawTabName, usedTabNames.Count == 0 ? "Default" : $"Tab {usedTabNames.Count + 1}");
                if (!usedTabNames.Add(safeTabName))
                {
                    continue;
                }

                if (!tabs.TryGetValue(rawTabName, out var apps) && !tabs.TryGetValue(safeTabName, out apps))
                {
                    apps = new List<AppEntry>();
                }

                var normalizedApps = NormalizeApps(apps);
                root.Tabs.Add(new AppTabData
                {
                    Name = safeTabName,
                    Apps = normalizedApps
                });
            }

            if (root.Tabs.Count == 0)
            {
                root.Tabs.Add(new AppTabData { Name = "Default", Apps = new List<AppDataItem>() });
            }

            var json = JsonSerializer.Serialize(root, JsonOptions());
            WriteFileAtomically(jsonPath, json);
            return new SaveResult
            {
                Success = true,
                Path = jsonPath
            };
        }
        catch (Exception ex)
        {
            return new SaveResult
            {
                Success = false,
                Path = jsonPath,
                ErrorMessage = ex.Message
            };
        }
    }

    public SaveResult CreateRecoveryBackup(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath))
            {
                return new SaveResult
                {
                    Success = true,
                    Path = string.Empty
                };
            }

            var backupPath = CreateRecoveryBackupPath(jsonPath);
            File.Copy(jsonPath, backupPath, overwrite: false);
            return new SaveResult
            {
                Success = true,
                Path = backupPath
            };
        }
        catch (Exception ex)
        {
            return new SaveResult
            {
                Success = false,
                Path = jsonPath,
                ErrorMessage = ex.Message
            };
        }
    }

    public string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return AppActions.Install;
        }

        return action.Trim() switch
        {
            AppActions.Install => AppActions.Install,
            AppActions.Uninstall => AppActions.Uninstall,
            AppActions.Pause => AppActions.Pause,
            _ => AppActions.Install
        };
    }

    public string NormalizeInstallMode(string? installMode)
    {
        if (string.IsNullOrWhiteSpace(installMode))
        {
            return InstallModes.SilentWithProgress;
        }

        return installMode.Trim() switch
        {
            InstallModes.Interactive => InstallModes.Interactive,
            InstallModes.Silent => InstallModes.Silent,
            InstallModes.SilentWithProgress => InstallModes.SilentWithProgress,
            _ => InstallModes.SilentWithProgress
        };
    }

    public SaveResult ExportPreset(string path, string presetName, IEnumerable<AppEntry> apps)
    {
        try
        {
            var payload = new PresetFileRoot
            {
                PresetName = NormalizeTabName(presetName, "Preset"),
                Apps = NormalizeApps(apps)
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions());
            WriteFileAtomically(path, json);
            return new SaveResult
            {
                Success = true,
                Path = path
            };
        }
        catch (Exception ex)
        {
            return new SaveResult
            {
                Success = false,
                Path = path,
                ErrorMessage = ex.Message
            };
        }
    }

    public PresetImportResult ImportPreset(string path, IReadOnlyCollection<string> existingPresetNames)
    {
        try
        {
            var rawText = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return CreateImportError(path, "Il file preset è vuoto.");
            }

            var payload = JsonSerializer.Deserialize<PresetFileRoot>(rawText, JsonOptions());
            if (payload == null)
            {
                return CreateImportError(path, "Il file preset non contiene dati validi.");
            }

            var baseName = NormalizeTabName(payload.PresetName, "Imported preset");
            var importedName = CreateUniqueImportedPresetName(baseName, existingPresetNames);
            var apps = new List<AppEntry>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in payload.Apps ?? new List<AppDataItem>())
            {
                var normalized = NormalizeApp(app, usedIds, trustAdvancedArguments: false);
                if (normalized != null)
                {
                    apps.Add(normalized);
                }
            }

            return new PresetImportResult
            {
                Success = true,
                Path = path,
                ImportedPresetName = importedName,
                Apps = apps
            };
        }
        catch (JsonException ex)
        {
            return CreateImportError(path, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return CreateImportError(path, ex.Message);
        }
        catch (IOException ex)
        {
            return CreateImportError(path, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateImportError(path, ex.Message);
        }
    }

    public string GetDefaultPresetExportFileName(string presetName)
    {
        var sanitized = string.Join(
            "-",
            NormalizeTabName(presetName, "preset")
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "preset";
        }

        return $"{sanitized}.onlywinget.json";
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private void LoadTabbedFormat(string rawText, Dictionary<string, List<AppEntry>> tabs, List<string> tabNames)
    {
        var root = JsonSerializer.Deserialize<AppDataRoot>(rawText, JsonOptions());
        if (root?.Tabs == null)
        {
            return;
        }

        var usedTabNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in root.Tabs)
        {
            var fallbackName = usedTabNames.Count == 0 ? "Default" : $"Tab {usedTabNames.Count + 1}";
            var tabName = NormalizeTabName(tab.Name, fallbackName);
            if (!usedTabNames.Add(tabName))
            {
                continue;
            }

            var list = new List<AppEntry>();
            if (tab.Apps != null)
            {
                var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var app in tab.Apps)
                {
                    var normalized = NormalizeApp(app, usedIds, trustAdvancedArguments: true);
                    if (normalized != null)
                    {
                        list.Add(normalized);
                    }
                }
            }

            tabs[tabName] = list;
            tabNames.Add(tabName);
        }
    }

    private AppEntry? NormalizeApp(AppDataItem? app, HashSet<string> usedIds, bool trustAdvancedArguments)
    {
        if (app == null)
        {
            return null;
        }

        var id = (app.Id ?? string.Empty).Trim();
        var architecture = NormalizeOptionalValue(app.Architecture);
        var operationKey = AppEntry.BuildOperationKey(id, architecture);
        if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(operationKey))
        {
            return null;
        }

        var name = (app.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = id;
        }

        var additionalCustomArgs = NormalizeOptionalValue(app.AdditionalCustomArgs);
        var overrideArgs = NormalizeOptionalValue(app.OverrideArgs);
        var hasAdvancedArguments = HasAdvancedArguments(additionalCustomArgs, overrideArgs);

        return new AppEntry
        {
            Enabled = app.Enabled,
            Name = name,
            Id = id,
            Source = NormalizeSource(app.Source),
            Version = (app.Version ?? string.Empty).Trim(),
            Action = NormalizeAction(app.Action),
            Scope = NormalizeOptionalValue(app.Scope),
            InstallMode = NormalizeInstallMode(app.InstallMode),
            Architecture = architecture,
            Locale = NormalizeOptionalValue(app.Locale),
            InstallerType = NormalizeOptionalValue(app.InstallerType),
            InstallLocation = NormalizeOptionalValue(app.InstallLocation),
            LogPath = NormalizeOptionalValue(app.LogPath),
            AdditionalCustomArgs = additionalCustomArgs,
            OverrideArgs = overrideArgs,
            AdvancedArgumentsReviewed = trustAdvancedArguments
                ? app.AdvancedArgumentsReviewed ?? true
                : !hasAdvancedArguments,
            ManifestFingerprint = NormalizeOptionalValue(app.ManifestFingerprint),
            InterrogatedAtUtc = NormalizeOptionalValue(app.InterrogatedAtUtc),
            ElevationRequirement = NormalizeOptionalValue(app.ElevationRequirement),
            Status = string.Empty
        };
    }

    private List<AppDataItem> NormalizeApps(IEnumerable<AppEntry>? apps)
    {
        var result = new List<AppDataItem>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (apps == null)
        {
            return result;
        }

        foreach (var app in apps)
        {
            var id = (app.Id ?? string.Empty).Trim();
            var architecture = NormalizeOptionalValue(app.Architecture);
            var operationKey = AppEntry.BuildOperationKey(id, architecture);
            if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(operationKey))
            {
                continue;
            }

            var name = (app.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = id;
            }

            result.Add(new AppDataItem
            {
                Enabled = app.Enabled,
                Name = name,
                Id = id,
                Action = NormalizeAction(app.Action),
                Source = NormalizeSource(app.Source),
                Version = NormalizeOptionalValue(app.Version),
                Scope = NormalizeOptionalValue(app.Scope),
                InstallMode = NormalizeInstallMode(app.InstallMode),
                Architecture = architecture,
                Locale = NormalizeOptionalValue(app.Locale),
                InstallerType = NormalizeOptionalValue(app.InstallerType),
                InstallLocation = NormalizeOptionalValue(app.InstallLocation),
                LogPath = NormalizeOptionalValue(app.LogPath),
                AdditionalCustomArgs = NormalizeOptionalValue(app.AdditionalCustomArgs),
                OverrideArgs = NormalizeOptionalValue(app.OverrideArgs),
                AdvancedArgumentsReviewed = app.AdvancedArgumentsReviewed,
                ManifestFingerprint = NormalizeOptionalValue(app.ManifestFingerprint),
                InterrogatedAtUtc = NormalizeOptionalValue(app.InterrogatedAtUtc),
                ElevationRequirement = NormalizeOptionalValue(app.ElevationRequirement)
            });
        }

        return result;
    }

    private static string NormalizeTabName(string? tabName, string fallback)
    {
        var safeName = (tabName ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? fallback : safeName;
    }

    private static string NormalizeSource(string? source)
    {
        var value = (source ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? "winget" : value;
    }

    private static string NormalizeOptionalValue(string? value) => (value ?? string.Empty).Trim();

    private static bool HasAdvancedArguments(string additionalCustomArgs, string overrideArgs)
    {
        return !string.IsNullOrWhiteSpace(additionalCustomArgs)
            || !string.IsNullOrWhiteSpace(overrideArgs);
    }

    private static PresetImportResult CreateImportError(string path, string message)
    {
        return new PresetImportResult
        {
            Success = false,
            Path = path,
            ErrorMessage = message
        };
    }

    private static string CreateUniqueImportedPresetName(string requestedName, IReadOnlyCollection<string> existingPresetNames)
    {
        var usedNames = new HashSet<string>(existingPresetNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!usedNames.Contains(requestedName))
        {
            return requestedName;
        }

        var importedName = $"{requestedName} (imported)";
        if (!usedNames.Contains(importedName))
        {
            return importedName;
        }

        var suffix = 2;
        while (usedNames.Contains($"{requestedName} (imported {suffix})"))
        {
            suffix++;
        }

        return $"{requestedName} (imported {suffix})";
    }

    private static void WriteFileAtomically(string jsonPath, string json)
    {
        var directory = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = jsonPath + ".tmp";
        var backupPath = jsonPath + ".bak";
        try
        {
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (File.Exists(jsonPath))
            {
                File.Replace(tempPath, jsonPath, backupPath, ignoreMetadataErrors: true);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            else
            {
                File.Move(tempPath, jsonPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string CreateRecoveryBackupPath(string jsonPath)
    {
        var directory = Path.GetDirectoryName(jsonPath);
        var fileName = Path.GetFileName(jsonPath);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var basePath = Path.Combine(
            string.IsNullOrWhiteSpace(directory) ? "." : directory,
            $"{fileName}.recovery-{timestamp}.bak");

        if (!File.Exists(basePath))
        {
            return basePath;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(
                string.IsNullOrWhiteSpace(directory) ? "." : directory,
                $"{fileName}.recovery-{timestamp}-{suffix}.bak");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to create a unique recovery backup path.");
    }

    private static void CreateDefaultTab(Dictionary<string, List<AppEntry>> tabs, List<string> tabNames)
    {
        tabs["Default"] = new List<AppEntry>();
        tabNames.Add("Default");
    }

    private static AppDataLoadResult CreateFallbackLoadResult(
        AppDataLoadStatus status,
        string jsonPath,
        string errorMessage)
    {
        var tabs = new Dictionary<string, List<AppEntry>>(StringComparer.OrdinalIgnoreCase);
        var tabNames = new List<string>();
        CreateDefaultTab(tabs, tabNames);
        return CreateLoadResult(status, jsonPath, tabs, tabNames, errorMessage);
    }

    private static AppDataLoadResult CreateLoadResult(
        AppDataLoadStatus status,
        string jsonPath,
        Dictionary<string, List<AppEntry>> tabs,
        List<string> tabNames,
        string errorMessage = "")
    {
        return new AppDataLoadResult
        {
            Status = status,
            Path = jsonPath,
            ErrorMessage = errorMessage,
            Tabs = tabs,
            TabNames = tabNames
        };
    }

}
