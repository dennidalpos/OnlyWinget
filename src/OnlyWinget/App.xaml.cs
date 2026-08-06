using System.Globalization;
using Microsoft.Extensions.Hosting;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Navigation;
using OnlyWinget.Application.System;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Services;

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

    internal static Microsoft.Extensions.Hosting.IHost Host { get; } = AppComposition.Host;

    internal static UiServiceCollection UiServices => AppComposition.CreateUiServices();

    public static OnlyWingetApplication Workflow => AppComposition.CreateWorkflow();

    public App()
    {
        Host.Start();
        ApplySettings();
        AppDiagnostics.Initialize();
        AppDiagnostics.Register(this);

        if (OperatingSystem.IsWindows())
        {
            var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                UrlProtocolRegistrationService.Register(exePath);
            }
        }

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

            var commandLineArgs = Environment.GetCommandLineArgs();
            var protocolUrl = commandLineArgs.FirstOrDefault(arg => arg.StartsWith("onlywinget://", StringComparison.OrdinalIgnoreCase));
            if (protocolUrl is not null)
            {
                var request = UrlProtocolParser.Parse(protocolUrl);
                if (request.IsValid)
                {
                    AppDiagnostics.Write("ProtocolActivation", $"Activated with action={request.Action}, packageId={request.PackageId}, query={request.Query}");
                }
            }
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("OnLaunched", exception);
            throw;
        }
    }
}
