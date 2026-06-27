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
        var processRunner = new ProcessExternalProcessRunner();
        var runner = new ProcessWingetCommandRunner(processRunner, new WingetProgressParser());
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        var capabilityService = new SystemCapabilityService(processRunner);

        return new OnlyWingetApplication(
            new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath),
            capabilityService,
            new WingetPackageSearchService(runner, parser, classifier),
            new WingetPackageResolver(runner, classifier),
            new WingetUpdateLoader(runner, parser, classifier),
            new PowerShellWindowsUpdateService(processRunner, capabilityService),
            new WingetSourceService(runner, parser, classifier),
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), classifier),
            sourcePreferenceStore: new JsonSourcePreferenceStore(JsonSourcePreferenceStore.DefaultFilePath));
    }
}
