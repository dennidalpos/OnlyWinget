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

    [Fact]
    public void BuildInstallArguments_RejectsUnreviewedAdvancedArguments()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Contoso.InternalTool",
            Source = "winget",
            AdditionalCustomArgs = "/unsafe",
            AdvancedArgumentsReviewed = false
        };

        var ex = Assert.Throws<InvalidOperationException>(() => builder.BuildInstallArguments(app));

        Assert.Contains("reviewed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildInstallArguments_ExpandsEnvironmentVariablePlaceholdersInAdvancedArguments()
    {
        var variableName = $"ONLYWINGET_TEST_TOKEN_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variableName, "expanded-token");
        try
        {
            var builder = CreateBuilder();
            var app = new AppEntry
            {
                Id = "Contoso.InternalTool",
                Source = "winget",
                AdditionalCustomArgs = $"/token %{variableName}%"
            };

            var args = builder.BuildInstallArguments(app);

            Assert.Contains("--custom", args);
            Assert.Contains("/token expanded-token", args);
            Assert.DoesNotContain($"/token %{variableName}%", args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public void BuildInstallArguments_ExpandsEnvironmentVariablePlaceholdersInPaths()
    {
        var variableName = $"ONLYWINGET_TEST_ROOT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variableName, @"C:\OnlyWingetTestRoot");
        try
        {
            var builder = CreateBuilder();
            var app = new AppEntry
            {
                Id = "Contoso.Portable",
                Source = "winget",
                InstallLocation = $@"%{variableName}%\Apps\Contoso",
                LogPath = $@"%{variableName}%\Logs\contoso.log"
            };

            var args = builder.BuildInstallArguments(app);

            Assert.Contains(@"C:\OnlyWingetTestRoot\Apps\Contoso", args);
            Assert.Contains(@"C:\OnlyWingetTestRoot\Logs\contoso.log", args);
            Assert.DoesNotContain($@"%{variableName}%\Apps\Contoso", args);
            Assert.DoesNotContain($@"%{variableName}%\Logs\contoso.log", args);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public void BuildInstallArguments_OmitsUnsupportedLocationAndLog()
    {
        var builder = CreateBuilder();
        var app = new AppEntry
        {
            Id = "Contoso.Tool",
            Source = "winget",
            InstallLocation = @"C:\Tools\Contoso",
            LogPath = @"C:\Logs\contoso.log",
            SupportsInstallLocation = false,
            SupportsLog = false
        };

        var args = builder.BuildInstallArguments(app);

        Assert.DoesNotContain("--location", args);
        Assert.DoesNotContain(@"C:\Tools\Contoso", args);
        Assert.DoesNotContain("--log", args);
        Assert.DoesNotContain(@"C:\Logs\contoso.log", args);
    }

    private static InstallCommandBuilder CreateBuilder()
    {
        var wingetService = new WingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = string.Empty },
            localRuntimeRoot: System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N")));
        return new InstallCommandBuilder(wingetService);
    }
}
