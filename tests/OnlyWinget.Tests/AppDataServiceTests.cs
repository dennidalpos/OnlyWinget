// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using System.Linq;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class AppDataServiceTests
{
    [Fact]
    public void Load_ReturnsDefaultTab_WhenJsonIsMalformed()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            File.WriteAllText(jsonPath, "{ invalid json");
            var service = new AppDataService(appDataRoot: root);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.InvalidData, result.Status);
            Assert.Single(result.TabNames);
            Assert.Equal("Default", result.TabNames[0]);
            Assert.Empty(result.Tabs["Default"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsInvalidData_WhenJsonUsesLegacyArrayFormat()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            File.WriteAllText(jsonPath, """[{ "Name": "VS Code", "Id": "Microsoft.VisualStudioCode", "Action": "Install" }]""");
            var service = new AppDataService(appDataRoot: root);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.InvalidData, result.Status);
            Assert.Single(result.TabNames);
            Assert.Equal("Default", result.TabNames[0]);
            Assert.Empty(result.Tabs["Default"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_NormalizesDuplicateIds_AndReportsTargetPath()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var jsonPath = Path.Combine(root, "AppsList.json");
            var result = service.Save(
                jsonPath,
                new[] { "Default" },
                new()
                {
                    ["Default"] =
                    [
                        new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install },
                        new AppEntry { Name = "Duplicate", Id = "Microsoft.VisualStudioCode", Action = AppActions.Uninstall }
                    ]
                });

            Assert.True(result.Success);
            Assert.Equal(jsonPath, result.Path);
            var loaded = service.Load(jsonPath);
            Assert.Single(loaded.Tabs["Default"]);
            Assert.Equal(AppActions.Install, loaded.Tabs["Default"].Single().Action);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsFileNotFoundStatus_WhenJsonDoesNotExist()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            var service = new AppDataService(appDataRoot: root);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.FileNotFound, result.Status);
            Assert.Single(result.TabNames);
            Assert.Equal("Default", result.TabNames[0]);
            Assert.Empty(result.Tabs["Default"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsIoErrorStatus_WhenFileIsLocked()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            File.WriteAllText(jsonPath, "{}");
            var service = new AppDataService(appDataRoot: root);

            using var lockHandle = new FileStream(jsonPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.IoError, result.Status);
            Assert.Single(result.TabNames);
            Assert.Equal("Default", result.TabNames[0]);
            Assert.Empty(result.Tabs["Default"]);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_AndLoad_PreservesExtendedInstallOptions()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var jsonPath = Path.Combine(root, "AppsList.json");
            var result = service.Save(
                jsonPath,
                new[] { "Default" },
                new()
                {
                    ["Default"] =
                    [
                        new AppEntry
                        {
                            Name = "PowerToys",
                            Id = "Microsoft.PowerToys",
                            Source = "winget",
                            Version = "0.98.1",
                            Action = AppActions.Install,
                            Scope = "machine",
                            InstallMode = InstallModes.Silent,
                            Architecture = "x64",
                            Locale = "en-US",
                            InstallerType = "burn",
                            InstallLocation = "C:\\Apps\\PowerToys",
                            LogPath = "C:\\Logs\\powertoys.log",
                            AdditionalCustomArgs = "/custom",
                            OverrideArgs = "/override",
                            ManifestFingerprint = "ABC123",
                            InterrogatedAtUtc = "2026-04-11T12:00:00.0000000Z",
                            ElevationRequirement = "elevationRequired"
                        }
                    ]
                });

            Assert.True(result.Success);

            var loaded = service.Load(jsonPath);
            var app = Assert.Single(loaded.Tabs["Default"]);
            Assert.Equal("winget", app.Source);
            Assert.Equal("0.98.1", app.Version);
            Assert.Equal("machine", app.Scope);
            Assert.Equal(InstallModes.Silent, app.InstallMode);
            Assert.Equal("x64", app.Architecture);
            Assert.Equal("en-US", app.Locale);
            Assert.Equal("burn", app.InstallerType);
            Assert.Equal("C:\\Apps\\PowerToys", app.InstallLocation);
            Assert.Equal("C:\\Logs\\powertoys.log", app.LogPath);
            Assert.Equal("/custom", app.AdditionalCustomArgs);
            Assert.Equal("/override", app.OverrideArgs);
            Assert.Equal("ABC123", app.ManifestFingerprint);
            Assert.Equal("2026-04-11T12:00:00.0000000Z", app.InterrogatedAtUtc);
            Assert.Equal("elevationRequired", app.ElevationRequirement);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Save_AndLoad_PreservesMultipleArchitecturesForSamePackageId()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var jsonPath = Path.Combine(root, "AppsList.json");

            var result = service.Save(
                jsonPath,
                new[] { "Default" },
                new()
                {
                    ["Default"] =
                    [
                        new AppEntry { Name = ".NET Runtime 8", Id = "Microsoft.DotNet.Runtime.8", Action = AppActions.Install, Architecture = "x64" },
                        new AppEntry { Name = ".NET Runtime 8", Id = "Microsoft.DotNet.Runtime.8", Action = AppActions.Install, Architecture = "x86" }
                    ]
                });

            Assert.True(result.Success);

            var loaded = service.Load(jsonPath);
            Assert.Equal(2, loaded.Tabs["Default"].Count);
            Assert.Contains(loaded.Tabs["Default"], app => app.Architecture == "x64");
            Assert.Contains(loaded.Tabs["Default"], app => app.Architecture == "x86");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_BackwardCompatible_WhenExtendedInstallFieldsAreMissing()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            File.WriteAllText(
                jsonPath,
                """
                {
                  "Tabs": [
                    {
                      "Name": "Default",
                      "Apps": [
                        {
                          "Name": "VS Code",
                          "Id": "Microsoft.VisualStudioCode",
                          "Action": "Install"
                        }
                      ]
                    }
                  ]
                }
                """);
            var service = new AppDataService(appDataRoot: root);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.Success, result.Status);
            var app = Assert.Single(result.Tabs["Default"]);
            Assert.Equal("winget", app.Source);
            Assert.Equal(InstallModes.SilentWithProgress, app.InstallMode);
            Assert.Equal(string.Empty, app.OverrideArgs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportPreset_WritesReadableJsonIncludingPresetName()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var exportPath = Path.Combine(root, "dev-tools.onlywinget.json");

            var result = service.ExportPreset(
                exportPath,
                "Dev Tools",
                new[]
                {
                    new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install }
                });

            Assert.True(result.Success);
            var json = File.ReadAllText(exportPath);
            Assert.Contains("\"presetName\": \"Dev Tools\"", json, StringComparison.Ordinal);
            Assert.Contains("\"apps\"", json, StringComparison.Ordinal);
            Assert.Contains("Microsoft.VisualStudioCode", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPreset_AddsPresetWithCollisionSafeName_AndNormalizedApps()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var importPath = Path.Combine(root, "preset.onlywinget.json");
            File.WriteAllText(
                importPath,
                """
                {
                  "formatVersion": 1,
                  "presetName": "Default",
                  "apps": [
                    { "name": "VS Code", "id": "Microsoft.VisualStudioCode", "action": "Install" },
                    { "name": "", "id": "Microsoft.VisualStudioCode", "action": "Uninstall" },
                    { "name": "", "id": "Git.Git", "action": "Unknown" }
                  ]
                }
                """);

            var result = service.ImportPreset(importPath, new[] { "Default", "Default (imported)" });

            Assert.True(result.Success);
            Assert.Equal("Default (imported 2)", result.ImportedPresetName);
            Assert.Equal(2, result.Apps.Count);
            Assert.Equal("VS Code", result.Apps[0].Name);
            Assert.Equal(AppActions.Install, result.Apps[0].Action);
            Assert.Equal("Git.Git", result.Apps[1].Name);
            Assert.Equal(AppActions.Install, result.Apps[1].Action);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPreset_ReturnsError_WhenJsonIsInvalid()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var importPath = Path.Combine(root, "broken.onlywinget.json");
            File.WriteAllText(importPath, "{ invalid json");

            var result = service.ImportPreset(importPath, Array.Empty<string>());

            Assert.False(result.Success);
            Assert.Equal(importPath, result.Path);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
