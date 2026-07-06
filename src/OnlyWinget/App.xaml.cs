using OnlyWinget.Application.App;
using OnlyWinget.Application.System;
using OnlyWinget.Services;
using System.Globalization;

namespace OnlyWinget;

public partial class App : Microsoft.UI.Xaml.Application
{
    private static Microsoft.UI.Xaml.Window? window;

    internal static nint WindowHandle => window is null ? 0 : WinRT.Interop.WindowNative.GetWindowHandle(window);

    internal static Microsoft.UI.WindowId WindowId => Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowHandle);
    internal static Microsoft.UI.Xaml.XamlRoot? XamlRoot => (window?.Content as Microsoft.UI.Xaml.FrameworkElement)?.XamlRoot;

    internal static void Navigate(string routeId)
    {
        if (window is MainWindow mainWindow) mainWindow.Navigate(routeId);
    }

    internal static UiServiceCollection UiServices { get; } = AppComposition.CreateUiServices();

    public static OnlyWingetApplication Workflow { get; } = AppComposition.CreateWorkflow();

    public App()
    {
        ApplySettings();
        AppDiagnostics.Initialize();
        AppDiagnostics.Register(this);
        InitializeComponent();
    }

    internal static void ApplySettings()
    {
        var settings = UiServices.Settings.Current;
        TextResources.OverrideCulture = settings.Language switch
        {
            "en" => CultureInfo.GetCultureInfo("en"),
            "it" => CultureInfo.GetCultureInfo("it"),
            _ => null
        };
        AppDiagnostics.IsEnabled = settings.DiagnosticLogging;
        AppDiagnostics.MinLogLevel = Enum.TryParse<AppLogLevel>(settings.LogLevel, out var level) ? level : AppLogLevel.Information;
        Workflow.ContinueOperationsAfterFailure = settings.ContinueOperationsAfterFailure;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("OnLaunched", exception);
            throw;
        }
    }

}
