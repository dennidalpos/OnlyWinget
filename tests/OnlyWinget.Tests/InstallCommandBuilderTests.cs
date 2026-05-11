// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Linq;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class InstallCommandBuilderTests
{
    [Fact]
    public void BuildInstallArguments_IncludesScopeAndCustomArgs()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Microsoft.PowerToys",
            Source = "winget",
            Scope = "machine",
            InstallMode = InstallModes.Silent,
            AdditionalCustomArgs = "/foo /bar"
        };

        var args = builder.BuildInstallArguments(app);

        Assert.Contains("install", args);
        Assert.Contains("--scope", args);
        Assert.Contains("machine", args);
        Assert.Contains("--custom", args);
        Assert.Contains("/foo /bar", args);
        Assert.Contains("--silent", args);
    }

    [Fact]
    public void BuildInstallArguments_UsesOverrideInsteadOfCustomArgs()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Git.Git",
            Source = "winget",
            AdditionalCustomArgs = "/custom",
            OverrideArgs = "/override"
        };

        var args = builder.BuildInstallArguments(app);

        Assert.Contains("--override", args);
        Assert.Contains("/override", args);
        Assert.DoesNotContain("--custom", args);
        Assert.DoesNotContain("/custom", args);
    }

    [Fact]
    public void BuildInstallArguments_InteractiveModeOmitsDisableInteractivity()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Microsoft.WindowsTerminal",
            Source = "winget",
            InstallMode = InstallModes.Interactive
        };

        var args = builder.BuildInstallArguments(app);

        Assert.Contains("--interactive", args);
        Assert.DoesNotContain("--disable-interactivity", args);
    }

    [Fact]
    public void BuildInstallArguments_PropagatesConfigurableInstallFields_WithoutPinningVersion()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "JRSoftware.InnoSetup",
            Source = "winget",
            Version = "6.3.3",
            Scope = "machine",
            Architecture = "x64",
            Locale = "en-US",
            InstallerType = "inno",
            InstallMode = InstallModes.Silent,
            InstallLocation = @"C:\tools\inno",
            LogPath = @"C:\logs\inno-install.log",
            AdditionalCustomArgs = "/norestart"
        };

        var args = builder.BuildInstallArguments(app);

        Assert.DoesNotContain("--version", args);
        Assert.DoesNotContain("6.3.3", args);
        Assert.Contains("--scope", args);
        Assert.Contains("machine", args);
        Assert.Contains("--architecture", args);
        Assert.Contains("x64", args);
        Assert.Contains("--locale", args);
        Assert.Contains("en-US", args);
        Assert.Contains("--installer-type", args);
        Assert.Contains("inno", args);
        Assert.Contains("--location", args);
        Assert.Contains(@"C:\tools\inno", args);
        Assert.Contains("--log", args);
        Assert.Contains(@"C:\logs\inno-install.log", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--custom", args);
        Assert.Contains("/norestart", args);
    }

    [Fact]
    public void BuildInstallArguments_OverrideArgsSuppressCustomArgs_WhenBothSet()
    {
        // When OverrideArgs is set, --custom must not appear; only --override is passed.
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Git.Git",
            Source = "winget",
            AdditionalCustomArgs = "/custom-should-not-appear",
            OverrideArgs = "/override-args"
        };

        var args = builder.BuildInstallArguments(app);

        Assert.Contains("--override", args);
        Assert.Contains("/override-args", args);
        Assert.DoesNotContain("--custom", args);
        Assert.DoesNotContain("/custom-should-not-appear", args);
    }

    private static InstallCommandBuilder CreateBuilder()
    {
        var wingetService = new WingetService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = string.Empty },
            localRuntimeRoot: System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N")));
        return new InstallCommandBuilder(wingetService);
    }
}
