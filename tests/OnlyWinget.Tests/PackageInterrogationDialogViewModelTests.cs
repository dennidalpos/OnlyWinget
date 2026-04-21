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
}
