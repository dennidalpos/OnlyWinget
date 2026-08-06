using System.Runtime.Versioning;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.WindowsUpdate;
using OnlyWinget.Infrastructure.Winget;
using Xunit;

namespace OnlyWinget.Tests;

[SupportedOSPlatform("windows")]
public class ComNativeServiceTests
{
    [Fact]
    public async Task ComWindowsUpdateService_FallbackScan_Succeeds()
    {
        var runner = new ProcessExternalProcessRunner();
        var capabilityService = new SystemCapabilityService(runner);
        var fallback = new PowerShellWindowsUpdateService(runner, capabilityService);
        var comService = new ComWindowsUpdateService(fallback);

        var outcome = await comService.ScanAsync(new WindowsUpdateOptions(false, false), CancellationToken.None);
        Assert.NotNull(outcome);
    }

    [Fact]
    public async Task ComWingetPackageService_FallbackSearch_Succeeds()
    {
        var runner = new ProcessExternalProcessRunner();
        var cmdRunner = new ProcessWingetCommandRunner(runner, new WingetProgressParser());
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();

        var searchService = new WingetPackageSearchService(cmdRunner, parser, classifier);
        var resolverService = new WingetPackageResolver(cmdRunner, parser, classifier);
        var comService = new ComWingetPackageService(searchService, resolverService);

        var result = await comService.SearchAsync(new PackageSearchRequest("nonexistentpackageid12345"), CancellationToken.None);
        Assert.NotNull(result);
    }
}
