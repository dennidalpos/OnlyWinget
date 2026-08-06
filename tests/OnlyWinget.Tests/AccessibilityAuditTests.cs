using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class AccessibilityAuditTests
{
    [Fact]
    public void PackageIdentity_ReturnsNonEmptyAccessibleDisplayString()
    {
        var identity = new PackageIdentity("Git.Git", "winget");

        Assert.False(string.IsNullOrWhiteSpace(identity.Id));
        Assert.False(string.IsNullOrWhiteSpace(identity.Source));
        Assert.Contains("Git.Git", identity.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationProgress_ProvidesAccessiblePhaseAndPercentage()
    {
        var progress = new OperationProgress("Git.Git", WingetProgressPhase.Installing, 75, 3, 4);

        Assert.Equal("Git.Git", progress.PackageId);
        Assert.Equal(WingetProgressPhase.Installing, progress.Phase);
        Assert.Equal(75, progress.Percentage);
        Assert.Equal(3, progress.CompletedPackages);
        Assert.Equal(4, progress.TotalPackages);
    }

    [Fact]
    public void Preset_ProvidesAccessibleSummaryProperties()
    {
        var package = new PackageIdentity("Microsoft.VisualStudioCode", "winget");
        var preset = new Preset("Developer-Tools", [package]);

        Assert.Equal("Developer-Tools", preset.Name);
        Assert.Single(preset.Packages);
        Assert.Equal(package, preset.Packages[0]);
    }

    [Fact]
    public void WingetRestPackageManifest_ProvidesAccessibleMetadata()
    {
        var manifest = new WingetRestPackageManifest(
            PackageIdentifier: "GitHub.CLI",
            PackageName: "GitHub CLI",
            Publisher: "GitHub",
            Author: "GitHub Inc",
            License: "MIT",
            ShortDescription: "GitHub's official command line tool",
            PackageVersions: ["2.50.0"]
        );

        Assert.Equal("GitHub.CLI", manifest.PackageIdentifier);
        Assert.Equal("GitHub CLI", manifest.PackageName);
        Assert.Equal("GitHub", manifest.Publisher);
        Assert.Equal("MIT", manifest.License);
        Assert.Single(manifest.PackageVersions);
    }
}
