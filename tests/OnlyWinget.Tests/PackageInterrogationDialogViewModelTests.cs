// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;
using OnlyWinget.Models;

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
}
