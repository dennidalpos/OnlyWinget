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
    public void Load_ReturnsInvalidData_WhenJsonFileIsTooLarge()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            WriteOversizedJsonFile(jsonPath);
            var service = new AppDataService(appDataRoot: root);

            var result = service.Load(jsonPath);

            Assert.Equal(AppDataLoadStatus.InvalidData, result.Status);
            Assert.Contains("dimensione", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Single(result.TabNames);
            Assert.Equal("Default", result.TabNames[0]);
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
    public void CreateRecoveryBackup_CopiesExistingLibraryWithoutReplacingIt()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var jsonPath = Path.Combine(root, "AppsList.json");
            const string originalContent = "{ invalid json";
            File.WriteAllText(jsonPath, originalContent);

            var result = service.CreateRecoveryBackup(jsonPath);

            Assert.True(result.Success);
            Assert.Equal(originalContent, File.ReadAllText(jsonPath));
            Assert.Equal(originalContent, File.ReadAllText(result.Path));
            Assert.StartsWith(
                Path.Combine(root, "AppsList.json.recovery-"),
                result.Path,
                StringComparison.Ordinal);
            Assert.EndsWith(".bak", result.Path, StringComparison.Ordinal);
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
    public void Save_AndLoad_PreservesExtendedInstallOptions_WithoutPersistingVersionMetadata()
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
                            Enabled = false,
                            Name = "PowerToys",
                            Id = "Microsoft.PowerToys",
                            Source = "winget",
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
                            ElevationRequirement = "elevationRequired"
                        }
                    ]
                });

            Assert.True(result.Success);

            var loaded = service.Load(jsonPath);
            var app = Assert.Single(loaded.Tabs["Default"]);
            Assert.False(app.Enabled);
            Assert.Equal("winget", app.Source);
            Assert.Equal("machine", app.Scope);
            Assert.Equal(InstallModes.Silent, app.InstallMode);
            Assert.Equal("x64", app.Architecture);
            Assert.Equal("en-US", app.Locale);
            Assert.Equal("burn", app.InstallerType);
            Assert.Equal("C:\\Apps\\PowerToys", app.InstallLocation);
            Assert.Equal("C:\\Logs\\powertoys.log", app.LogPath);
            Assert.Equal("/custom", app.AdditionalCustomArgs);
            Assert.Equal("/override", app.OverrideArgs);
            Assert.True(app.AdvancedArgumentsReviewed);
            Assert.Equal("elevationRequired", app.ElevationRequirement);

            var json = File.ReadAllText(jsonPath);
            Assert.DoesNotContain("\"Version\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ManifestFingerprint\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"InterrogatedAtUtc\"", json, StringComparison.Ordinal);
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
    public void Save_AndLoad_PreservesSamePackageIdAndArchitectureAcrossSources()
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
                        new AppEntry { Name = "Contoso Tool", Id = "Contoso.Tool", Source = "winget", Action = AppActions.Install, Architecture = "x64" },
                        new AppEntry { Name = "Contoso Tool", Id = "Contoso.Tool", Source = "msstore", Action = AppActions.Install, Architecture = "x64" }
                    ]
                });

            Assert.True(result.Success);

            var loaded = service.Load(jsonPath);
            Assert.Equal(2, loaded.Tabs["Default"].Count);
            Assert.Contains(loaded.Tabs["Default"], app => app.Source == "winget" && app.Architecture == "x64");
            Assert.Contains(loaded.Tabs["Default"], app => app.Source == "msstore" && app.Architecture == "x64");
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
            Assert.True(app.Enabled);
            Assert.Equal(InstallModes.SilentWithProgress, app.InstallMode);
            Assert.Equal(string.Empty, app.OverrideArgs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportPreset_WritesFullJsonWithInstallerSpecificFields()
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
                    new AppEntry
                    {
                        Name = "VS Code",
                        Id = "Microsoft.VisualStudioCode",
                        Source = "winget",
                        Action = AppActions.Install,
                        Scope = "machine",
                        InstallMode = InstallModes.Silent,
                        Architecture = "x64",
                        Locale = "en-US",
                        InstallerType = "inno",
                        InstallLocation = @"C:\Apps\VSCode",
                        LogPath = @"C:\Logs\vscode.log",
                        AdditionalCustomArgs = "/custom",
                        OverrideArgs = "/override",
                        ElevationRequirement = "elevationRequired"
                    }
                });

            Assert.True(result.Success);
            var json = File.ReadAllText(exportPath);
            Assert.Contains("\"presetName\": \"Dev Tools\"", json, StringComparison.Ordinal);
            Assert.Contains("\"apps\"", json, StringComparison.Ordinal);
            Assert.Contains("\"id\": \"Microsoft.VisualStudioCode\"", json, StringComparison.Ordinal);
            Assert.Contains("\"source\": \"winget\"", json, StringComparison.Ordinal);
            Assert.Contains("\"scope\": \"machine\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"installMode\": \"silent\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"architecture\": \"x64\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"locale\": \"en-US\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"installerType\": \"inno\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"installLocation\": \"C:\\\\Apps\\\\VSCode\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"logPath\": \"C:\\\\Logs\\\\vscode.log\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"additionalCustomArgs\": \"/custom\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"overrideArgs\": \"/override\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"elevationRequirement\": \"elevationRequired\"", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPreset_ReturnsExistingPresetNameForOverwrite_AndNormalizedApps()
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
            Assert.Equal("Default", result.ImportedPresetName);
            Assert.Equal(2, result.Apps.Count);
            Assert.Equal("VS Code", result.Apps[0].Name);
            Assert.Equal(AppActions.Install, result.Apps[0].Action);
            Assert.Equal("Git.Git", result.Apps[1].Name);
            Assert.Equal(AppActions.Install, result.Apps[1].Action);
            Assert.True(result.Apps[1].AdvancedArgumentsReviewed);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPreset_PreservesInstallerSpecificFields_AndRequiresReviewForAdvancedArguments()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var importPath = Path.Combine(root, "advanced.onlywinget.json");
            File.WriteAllText(
                importPath,
                """
                {
                  "formatVersion": 1,
                  "presetName": "Advanced",
                  "apps": [
                    {
                      "name": "Internal Tool",
                      "id": "Contoso.InternalTool",
                      "action": "Install",
                      "scope": "machine",
                      "installMode": "silent",
                      "architecture": "x64",
                      "locale": "en-US",
                      "installerType": "wix",
                      "installLocation": "C:\\Tools\\Internal",
                      "logPath": "C:\\Logs\\internal.log",
                      "additionalCustomArgs": "/unsafe",
                      "overrideArgs": "/override",
                      "advancedArgumentsReviewed": true
                    },
                    {
                      "name": "Plain Tool",
                      "id": "Contoso.PlainTool",
                      "action": "Install"
                    }
                  ]
                }
                """);

            var result = service.ImportPreset(importPath, Array.Empty<string>());

            Assert.True(result.Success);
            var advanced = Assert.Single(result.Apps, app => app.Id == "Contoso.InternalTool");
            Assert.Equal("machine", advanced.Scope);
            Assert.Equal(InstallModes.Silent, advanced.InstallMode);
            Assert.Equal("x64", advanced.Architecture);
            Assert.Equal("en-US", advanced.Locale);
            Assert.Equal("wix", advanced.InstallerType);
            Assert.Equal("C:\\Tools\\Internal", advanced.InstallLocation);
            Assert.Equal("C:\\Logs\\internal.log", advanced.LogPath);
            Assert.Equal("/unsafe", advanced.AdditionalCustomArgs);
            Assert.Equal("/override", advanced.OverrideArgs);
            Assert.False(advanced.AdvancedArgumentsReviewed);
            Assert.True(advanced.RequiresAdvancedArgumentsReview);
            var plain = Assert.Single(result.Apps, app => app.Id == "Contoso.PlainTool");
            Assert.True(plain.AdvancedArgumentsReviewed);
            Assert.False(plain.RequiresAdvancedArgumentsReview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPreset_DeduplicatesFullIdsWithInstallerArchitecture()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var importPath = Path.Combine(root, "portable.onlywinget.json");
            File.WriteAllText(
                importPath,
                """
                {
                  "formatVersion": 1,
                  "presetName": "Portable",
                  "apps": [
                    { "name": ".NET Runtime x64", "id": "Microsoft.DotNet.Runtime.8", "source": "winget", "architecture": "x64" },
                    { "name": ".NET Runtime x86", "id": "Microsoft.DotNet.Runtime.8", "source": "winget", "architecture": "x86" },
                    { "name": ".NET Runtime Store", "id": "Microsoft.DotNet.Runtime.8", "source": "msstore", "architecture": "x64" }
                  ]
                }
                """);

            var result = service.ImportPreset(importPath, Array.Empty<string>());

            Assert.True(result.Success);
            Assert.Equal(3, result.Apps.Count);
            Assert.Contains(result.Apps, app => app.Id == "Microsoft.DotNet.Runtime.8" && app.Source == "winget" && app.Architecture == "x64");
            Assert.Contains(result.Apps, app => app.Id == "Microsoft.DotNet.Runtime.8" && app.Source == "winget" && app.Architecture == "x86");
            Assert.Contains(result.Apps, app => app.Id == "Microsoft.DotNet.Runtime.8" && app.Source == "msstore" && app.Architecture == "x64");
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

    [Fact]
    public void ImportPreset_ReturnsError_WhenJsonFileIsTooLarge()
    {
        var root = CreateTempDirectory();
        try
        {
            var service = new AppDataService(appDataRoot: root);
            var importPath = Path.Combine(root, "oversized.onlywinget.json");
            WriteOversizedJsonFile(importPath);

            var result = service.ImportPreset(importPath, Array.Empty<string>());

            Assert.False(result.Success);
            Assert.Equal(importPath, result.Path);
            Assert.Contains("dimensione", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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

    private static void WriteOversizedJsonFile(string path)
    {
        File.WriteAllText(path, new string(' ', (5 * 1024 * 1024) + 1));
    }
}
