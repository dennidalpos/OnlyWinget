using OnlyWinget.Application.App;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.Winget;
using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget;

internal static class AppComposition
{
    public static OnlyWingetApplication CreateWorkflow()
    {
        var runner = new ProcessWingetCommandRunner();
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        var capabilityService = new SystemCapabilityService(runner);

        return new OnlyWingetApplication(
            new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath),
            capabilityService,
            new WingetPackageSearchService(runner, parser, classifier),
            new WingetPackageResolver(runner, classifier),
            new WingetUpdateLoader(runner, parser, classifier),
            new PowerShellWindowsUpdateService(runner, capabilityService),
            new WingetSourceService(runner, parser, classifier),
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), classifier));
    }
}
