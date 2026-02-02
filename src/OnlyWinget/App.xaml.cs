using System;
using System.Windows;
using System.Windows.Threading;
using OnlyWinget.ViewModels;

namespace OnlyWinget;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var viewModel = new MainViewModel();
        if (!viewModel.IsWingetAvailable)
        {
            MessageBox.Show(viewModel.Strings.WingetNotFoundText, viewModel.Strings.WingetNotFoundTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown();
    }
}
