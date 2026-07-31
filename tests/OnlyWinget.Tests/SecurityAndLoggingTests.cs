using OnlyWinget.Application.System;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class SecurityAndLoggingTests
{
    [Fact]
    public void WingetCommandBuilder_ValidInput_BuildsArgumentsSuccessfully()
    {
        var builder = new WingetCommandBuilder();
        var selection = new PackageSelection(
            new PackageIdentity("Microsoft.VisualStudioCode", "winget"),
            PackageAction.Install);

        var args = builder.Build(selection);

        Assert.Contains("install", args);
        Assert.Contains("--id", args);
        Assert.Contains("Microsoft.VisualStudioCode", args);
        Assert.Contains("--source", args);
        Assert.Contains("winget", args);
    }

    [Theory]
    [InlineData("Package;rmdir")]
    [InlineData("Package|calc")]
    [InlineData("Package&whoami")]
    [InlineData("Package\"test")]
    [InlineData("Package'test")]
    [InlineData("Package`test")]
    [InlineData("Package\nline")]
    public void WingetCommandBuilder_InvalidCharacters_ThrowsArgumentException(string maliciousId)
    {
        var builder = new WingetCommandBuilder();
        var selection = new PackageSelection(
            new PackageIdentity(maliciousId, "winget"),
            PackageAction.Install);

        Assert.Throws<ArgumentException>(() => builder.Build(selection));
    }

    [Fact]
    public void SystemCapabilities_ElevationProperty_DefaultsAndAssignsCorrectly()
    {
        var caps = new SystemCapabilities(
            IsSupportedOs: true,
            IsWingetAvailable: true,
            IsPowerShellAvailable: true,
            IsWindowsUpdateComAvailable: true,
            WindowsUpdateUnavailableReason: null,
            WingetVersion: "v1.8.0",
            WindowsBuildNumber: 19045,
            IsElevated: true);

        Assert.True(caps.IsElevated);
        Assert.True(caps.CanUseWinget);
        Assert.True(caps.CanUseWindowsUpdate);
    }
}
