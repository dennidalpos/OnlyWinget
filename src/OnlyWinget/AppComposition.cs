using OnlyWinget.Application.App;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.Winget;
using OnlyWinget.Infrastructure.WindowsUpdate;
using OnlyWinget.Services;
using OnlyWinget.Shell;

namespace OnlyWinget;

internal static class AppComposition
{
    public static UiServiceCollection CreateUiServices()
    {
        var settings = new JsonAppSettingsService(JsonAppSettingsService.DefaultFilePath);
        return new(
            settings,
            new ConfirmationService(settings),
            new FilePickerService(),
            new ClipboardService(),
            new NavigationRegistry());
    }

    public static OnlyWingetApplication CreateWorkflow()
    {
        var processRunner = new ProcessExternalProcessRunner();
        var runner = new ProcessWingetCommandRunner(processRunner, new WingetProgressParser());
        var parser = new WingetTableParser();
        var classifier = new WingetErrorClassifier();
        var capabilityService = new SystemCapabilityService(processRunner);

        var app = new OnlyWingetApplication(
            new JsonWorkspaceStore(JsonWorkspaceStore.DefaultFilePath),
            capabilityService,
            new WingetPackageSearchService(runner, parser, classifier),
            new WingetPackageResolver(runner, classifier),
            new WingetUpdateLoader(runner, parser, classifier),
            new PowerShellWindowsUpdateService(processRunner, capabilityService),
            new WingetSourceService(runner, parser, classifier),
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), classifier),
            sourcePreferenceStore: new JsonSourcePreferenceStore(JsonSourcePreferenceStore.DefaultFilePath));

        app.ExceptionLogger = AppDiagnostics.WriteException;
        return app;
    }
}
