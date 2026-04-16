// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Windows;
using System.Windows.Threading;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using System.Net.Http;

namespace OnlyWinget;

public partial class App : Application
{
    private LocalizationService? _localizationService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var preferencesService = new AppPreferencesService();
        var localizationService = new LocalizationService(preferencesService);
        _localizationService = localizationService;
        var wingetService = new WingetService();
        var installCommandBuilder = new InstallCommandBuilder(wingetService);
        var interrogationService = new WingetPackageInterrogationService(wingetService, new HttpClient());
        var wingetQueryService = new WingetQueryService(wingetService);
        var dataService = new AppDataService();
        var presetWorkspaceService = new PresetWorkspaceService(dataService);
        var dialogService = new DialogService(interrogationService, localizationService);
        var appEntryService = new AppEntryService(wingetService);
        var tabService = new TabService();
        var operationRunner = new OperationRunner(wingetService, installCommandBuilder);
        var updatesWorkspaceService = new UpdatesWorkspaceService(wingetQueryService, operationRunner);
        var startupCoordinator = new AppStartupCoordinator(wingetService, dialogService);

        var viewModel = new MainViewModel(
            wingetQueryService,
            presetWorkspaceService,
            localizationService,
            dialogService,
            appEntryService,
            tabService,
            operationRunner,
            updatesWorkspaceService);

        if (!startupCoordinator.CanContinueStartup(viewModel))
        {
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = mainWindow;
        viewModel.Initialize();
        mainWindow.Show();

        _ = startupCoordinator.RunPostStartupChecksAsync(viewModel);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var message = e.Exception.InnerException?.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = e.Exception.Message;
        }

        var strings = _localizationService?.Strings ?? new LocalizationService().Strings;
        MessageBox.Show(message, strings.UnhandledErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown();
    }
}
