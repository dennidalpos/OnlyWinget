// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class AppPreferencesServiceTests : IDisposable
{
    private readonly string _root;

    public AppPreferencesServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Load_ReturnsDefaultPreferences_WhenSettingsFileIsMissing()
    {
        var service = new AppPreferencesService(_root);

        var result = service.Load();

        Assert.Equal(string.Empty, result.PreferredUiLanguage);
    }

    [Fact]
    public void Load_ReturnsDefaultPreferences_WhenSettingsFileIsInvalid()
    {
        var service = new AppPreferencesService(_root);
        File.WriteAllText(service.GetSettingsPath(), "{ invalid json");

        var result = service.Load();

        Assert.Equal(string.Empty, result.PreferredUiLanguage);
    }

    [Fact]
    public void Save_AndLoad_PreservePreferredUiLanguage()
    {
        var service = new AppPreferencesService(_root);
        service.Save(new AppPreferences
        {
            PreferredUiLanguage = "en"
        });

        var result = service.Load();

        Assert.Equal("en", result.PreferredUiLanguage);
    }

    [Fact]
    public void Save_RewritesSettingsWithoutLeavingTemporaryArtifacts()
    {
        var service = new AppPreferencesService(_root);
        service.Save(new AppPreferences
        {
            PreferredUiLanguage = "it"
        });

        service.Save(new AppPreferences
        {
            PreferredUiLanguage = "en"
        });

        var settingsPath = service.GetSettingsPath();
        Assert.True(File.Exists(settingsPath));
        Assert.False(File.Exists(settingsPath + ".tmp"));
        Assert.False(File.Exists(settingsPath + ".bak"));
        Assert.Equal("en", service.Load().PreferredUiLanguage);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }
}
