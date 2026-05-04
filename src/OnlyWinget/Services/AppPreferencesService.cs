// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class AppPreferencesService
{
    private readonly string _appDataRoot;

    public AppPreferencesService(string? appDataRoot = null)
    {
        _appDataRoot = appDataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnlyWinget");
    }

    public string GetSettingsPath()
    {
        return Path.Combine(_appDataRoot, "settings.json");
    }

    public AppPreferences Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new AppPreferences();
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppPreferences();
            }

            return JsonSerializer.Deserialize<AppPreferences>(json, JsonOptions()) ?? new AppPreferences();
        }
        catch (JsonException)
        {
            return new AppPreferences();
        }
        catch (NotSupportedException)
        {
            return new AppPreferences();
        }
        catch (IOException)
        {
            return new AppPreferences();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences preferences)
    {
        try
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(preferences ?? new AppPreferences(), JsonOptions());
            WriteFileAtomically(path, json);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static void WriteFileAtomically(string path, string json)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";
        try
        {
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
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
}
