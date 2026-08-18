using System.Runtime.Versioning;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.WindowsUpdate;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class LiveUpdateSearchSmokeTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task WingetSearchAndUpdateDiscoveryCompleteAgainstLiveSource()
    {
        if (!ShouldRun())
        {
            return;
        }

        var processRunner = new ProcessExternalProcessRunner();
        var commandRunner = new ProcessWingetCommandRunner(processRunner, new WingetProgressParser());
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        var search = new WingetPackageSearchService(commandRunner, parser, classifier);
        var updates = new WingetUpdateLoader(commandRunner, parser, classifier);

        var searchOutcome = await search.SearchAsync(
            new PackageSearchRequest("powertoys", "winget"),
            CancellationToken.None);
        var updateOutcome = await updates.LoadUpdatesAsync("winget", CancellationToken.None);

        Assert.True(searchOutcome.Succeeded, searchOutcome.Error?.Message);
        Assert.Contains(searchOutcome.Rows, row =>
            string.Equals(row.Package.Id, "Microsoft.PowerToys", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            updateOutcome.Succeeded || updateOutcome.Error?.Kind == WingetErrorKind.NoUpdates,
            updateOutcome.Error?.Message);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task WindowsUpdateDiscoveryCompletesWithoutElevation()
    {
        if (!ShouldRun())
        {
            return;
        }

        var processRunner = new ProcessExternalProcessRunner();
        ISystemCapabilityService capabilities = new SystemCapabilityService(processRunner);
        var service = new PowerShellWindowsUpdateService(processRunner, capabilities);

        var outcome = await service.ScanAsync(
            new WindowsUpdateOptions(IncludeSoftware: true, IncludeDrivers: true),
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Error?.Message);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    [SupportedOSPlatform("windows")]
    public async Task ComWingetPackageServiceSearchesLiveCatalogThroughComOrCliFallback()
    {
        if (!ShouldRun())
        {
            return;
        }

        var processRunner = new ProcessExternalProcessRunner();
        var commandRunner = new ProcessWingetCommandRunner(processRunner, new WingetProgressParser());
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        var search = new WingetPackageSearchService(commandRunner, parser, classifier);
        var resolver = new WingetPackageResolver(commandRunner, parser, classifier);
        var service = new ComWingetPackageService(search, resolver);

        var outcome = await service.SearchAsync(
            new PackageSearchRequest("powertoys", "winget"),
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Error?.Message);
        Assert.Contains(outcome.Rows, row =>
            string.Equals(row.Package.Id, "Microsoft.PowerToys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    [SupportedOSPlatform("windows")]
    public async Task ComWindowsUpdateServiceScansLiveSystemThroughComOrPowerShellFallback()
    {
        if (!ShouldRun())
        {
            return;
        }

        var processRunner = new ProcessExternalProcessRunner();
        ISystemCapabilityService capabilities = new SystemCapabilityService(processRunner);
        var fallback = new PowerShellWindowsUpdateService(processRunner, capabilities);
        var service = new ComWindowsUpdateService(fallback);

        var outcome = await service.ScanAsync(
            new WindowsUpdateOptions(IncludeSoftware: true, IncludeDrivers: true),
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Error?.Message);
    }

    private static bool ShouldRun() =>
        string.Equals(Environment.GetEnvironmentVariable("ONLYWINGET_RUN_WINGET_SMOKE"), "1", StringComparison.Ordinal);
}
