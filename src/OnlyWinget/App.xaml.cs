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
    private SingleInstanceGuard? _singleInstanceGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var preferencesService = new AppPreferencesService();
        var localizationService = new LocalizationService(preferencesService);
        _localizationService = localizationService;
        var singleInstanceGuard = new SingleInstanceGuard();
        if (!singleInstanceGuard.TryAcquire())
        {
            MessageBox.Show(
                localizationService.Strings.SingleInstanceText,
                localizationService.Strings.SingleInstanceTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            singleInstanceGuard.Dispose();
            Shutdown();
            return;
        }

        _singleInstanceGuard = singleInstanceGuard;

        var operatingSystemInfo = new OperatingSystemInfoService().Detect();
        var wingetService = new WingetCommandService();
        var wingetQueryService = new WingetQueryService(wingetService);
        var installCommandBuilder = new InstallCommandBuilder(wingetService);
        var interrogationService = new WingetPackageInterrogationService(wingetService, new HttpClient(), operatingSystemInfo: operatingSystemInfo, wingetQueryService: wingetQueryService);
        var dataService = new AppDataService();
        var dialogService = new DialogService(interrogationService, localizationService);
        var appEntryService = new AppEntryService(wingetQueryService);
        var tabService = new TabService();
        var operationService = new PackageOperationService(wingetService, installCommandBuilder, wingetQueryService: wingetQueryService);
        var operationRunner = new OperationRunner(wingetService, installCommandBuilder, operationService: operationService);
        var startupCoordinator = new AppStartupCoordinator(wingetService, dialogService, operationService: operationService);

        var viewModel = new MainViewModel(
            wingetService,
            dataService,
            localizationService,
            dialogService,
            appEntryService,
            tabService,
            operationRunner,
            wingetQueryService,
            operatingSystemInfo);

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

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceGuard?.Dispose();
        _singleInstanceGuard = null;
        base.OnExit(e);
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
