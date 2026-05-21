// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Linq;
using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class PackageInterrogationDialogViewModelTests
{
    [Fact]
    public void Constructor_UsesEnglishCatalogText()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);

        Assert.Equal("Package interrogation", viewModel.Title);
        Assert.Equal("Add to queue", viewModel.ConfirmLabel);
        Assert.Equal("Unsupported args", viewModel.UnsupportedArgumentsLabel);
        Assert.Equal("Name", viewModel.PackageNameLabel);
        Assert.Equal("Detected installer type", viewModel.DetectedInstallerTypeLabel);
    }

    [Fact]
    public void Constructor_UsesItalianCatalogText()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.Italian);

        Assert.Equal("Interrogazione pacchetto", viewModel.Title);
        Assert.Equal("Aggiungi in coda", viewModel.ConfirmLabel);
        Assert.Equal("Argomenti non supportati", viewModel.UnsupportedArgumentsLabel);
        Assert.Equal("Nome", viewModel.PackageNameLabel);
        Assert.Equal("Tipo installer rilevato", viewModel.DetectedInstallerTypeLabel);
    }

    [Fact]
    public void ApplyInterrogationResult_MarksContentReadyAfterLoading()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);

        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.IsContentReady);
        Assert.False(viewModel.CanConfirm);

        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Git.Git",
            Name = "Git",
            Version = "2.53.0",
            Source = "winget",
            InstallerType = "exe",
            DefaultSelection = new SelectedInstallOptions()
        });

        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.IsContentReady);
        Assert.True(viewModel.CanConfirm);
    }

    [Fact]
    public void BuildSelections_ReturnsOneEntryPerSelectedArchitecture_InAddMode()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);

        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Microsoft.DotNet.Runtime.8",
            Name = ".NET Runtime 8",
            Version = "8.0.16",
            Source = "winget",
            InstallerType = "exe",
            InstallerOptions =
            [
                new ResolvedInstallerOption { Architecture = "x64", SupportsSilent = true, SupportsSilentWithProgress = true },
                new ResolvedInstallerOption { Architecture = "x86", SupportsSilent = true, SupportsSilentWithProgress = true }
            ],
            AvailableArchitectures = ["x64", "x86"],
            AvailableInstallModes = [InstallModes.Interactive, InstallModes.Silent, InstallModes.SilentWithProgress],
            DefaultSelection = new SelectedInstallOptions
            {
                Architecture = "x64",
                InstallMode = InstallModes.SilentWithProgress,
                LogPath = @"C:\logs\dotnet-runtime.log"
            }
        });

        Assert.True(viewModel.IsArchitectureMultiSelectVisible);
        viewModel.AvailableArchitectureOptions.Single(option => option.Value == "x86").IsSelected = true;

        var selections = viewModel.BuildSelections();

        Assert.Equal(2, selections.Count);
        Assert.Contains(selections, selection => selection.Architecture == "x64");
        Assert.Contains(selections, selection => selection.Architecture == "x86");
        Assert.Contains("--architecture x64", viewModel.CommandPreview);
        Assert.Contains("--architecture x86", viewModel.CommandPreview);
    }

    [Fact]
    public void ApplyExistingEntry_SwitchesArchitectureBackToSingleSelect_InEditMode()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);
        viewModel.ConfigureForEditMode(true);

        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Microsoft.DotNet.Runtime.8",
            Name = ".NET Runtime 8",
            Version = "8.0.16",
            Source = "winget",
            InstallerType = "exe",
            InstallerOptions =
            [
                new ResolvedInstallerOption { Architecture = "x64", SupportsSilent = true, SupportsSilentWithProgress = true },
                new ResolvedInstallerOption { Architecture = "x86", SupportsSilent = true, SupportsSilentWithProgress = true }
            ],
            AvailableArchitectures = ["x64", "x86"],
            AvailableInstallModes = [InstallModes.Interactive, InstallModes.Silent, InstallModes.SilentWithProgress],
            DefaultSelection = new SelectedInstallOptions
            {
                Architecture = "x64",
                InstallMode = InstallModes.SilentWithProgress
            }
        });

        viewModel.ApplyExistingEntry(new AppEntry { Architecture = "x86" });
        var selection = viewModel.BuildSelection();

        Assert.False(viewModel.IsArchitectureMultiSelectVisible);
        Assert.True(viewModel.IsArchitectureSingleSelectVisible);
        Assert.Equal("x86", selection.Architecture);
        Assert.Single(viewModel.BuildSelections());
    }

    [Fact]
    public void CommandPreview_RedactsAdvancedArguments_AndShowsPlainTextWarning()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);
        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Contoso.InternalTool",
            Name = "Internal Tool",
            Version = "1.0.0",
            Source = "winget",
            InstallerType = "exe",
            DefaultSelection = new SelectedInstallOptions
            {
                AdditionalCustomArgs = "/token super-secret-token"
            }
        });

        Assert.True(viewModel.ShowAdvancedArgumentsWarning);
        Assert.Contains("plain text", viewModel.AdvancedArgumentsWarningText);
        Assert.Contains("--custom [redacted]", viewModel.CommandPreview);
        Assert.DoesNotContain("super-secret-token", viewModel.CommandPreview);
    }

    [Fact]
    public void CanConfirm_AllowsPortableArchiveWithScopeFromAnotherInstallerNode()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);
        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "VideoLAN.VLC",
            Name = "VLC media player",
            Version = "3.0.23",
            Source = "winget",
            InstallerType = "nullsoft",
            InstallerOptions =
            [
                new ResolvedInstallerOption { Architecture = "x64", Scope = "machine", InstallerType = "nullsoft", SupportsSilent = true },
                new ResolvedInstallerOption { Architecture = "x64", InstallerType = "zip", SupportsSilent = true }
            ],
            AvailableScopes = ["machine", "user"],
            AvailableArchitectures = ["x64"],
            AvailableInstallerTypes = ["nullsoft", "zip"],
            DefaultSelection = new SelectedInstallOptions
            {
                Scope = "machine",
                Architecture = "x64",
                InstallerType = "zip",
                InstallMode = InstallModes.Silent
            }
        });

        Assert.True(viewModel.CanConfirm);
        Assert.True(viewModel.IsLocationSupported);
        Assert.True(viewModel.IsLogSupported);
        Assert.Contains("--scope machine", viewModel.CommandPreview);
        Assert.Contains("--installer-type zip", viewModel.CommandPreview);
    }

    [Fact]
    public void BuildSelection_OmitsUnsupportedLocationAndLog()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);
        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Contoso.Tool",
            Name = "Tool",
            Version = "1.0.0",
            Source = "winget",
            InstallerType = "exe",
            InstallerOptions =
            [
                new ResolvedInstallerOption
                {
                    Architecture = "x64",
                    InstallerType = "exe",
                    SupportsSilent = true,
                    UnsupportedArguments = ["Location", "Log"]
                }
            ],
            AvailableArchitectures = ["x64"],
            AvailableInstallerTypes = ["exe"],
            DefaultSelection = new SelectedInstallOptions
            {
                Architecture = "x64",
                InstallerType = "exe",
                InstallLocation = @"C:\Tools\Contoso",
                LogPath = @"C:\Logs\contoso.log"
            }
        });

        var selection = viewModel.BuildSelection();

        Assert.False(selection.SupportsInstallLocation);
        Assert.False(selection.SupportsLog);
        Assert.Equal(string.Empty, selection.InstallLocation);
        Assert.Equal(string.Empty, selection.LogPath);
        Assert.DoesNotContain("--location", viewModel.CommandPreview);
        Assert.DoesNotContain("--log", viewModel.CommandPreview);
    }

    [Fact]
    public void InstallLocationPreset_UsesEnvironmentVariablePath()
    {
        var viewModel = new PackageInterrogationDialogViewModel(LocalizedStrings.English);
        viewModel.ApplyInterrogationResult(new PackageInterrogationResult
        {
            Success = true,
            Id = "Contoso.Tool",
            Name = "Tool",
            Version = "1.0.0",
            Source = "winget",
            InstallerType = "portable",
            InstallerOptions =
            [
                new ResolvedInstallerOption { Architecture = "x64", InstallerType = "portable", SupportsSilent = true }
            ],
            AvailableArchitectures = ["x64"],
            AvailableInstallerTypes = ["portable"],
            DefaultSelection = new SelectedInstallOptions { Architecture = "x64", InstallerType = "portable" }
        });

        var desktopPreset = Assert.Single(viewModel.InstallLocationPresets, value => value.StartsWith(@"%USERPROFILE%\Desktop\", StringComparison.OrdinalIgnoreCase));
        viewModel.SelectedInstallLocationPreset = desktopPreset;

        Assert.Equal(desktopPreset, viewModel.InstallLocation);
        Assert.Equal(desktopPreset, viewModel.BuildSelection().InstallLocation);
    }
}
