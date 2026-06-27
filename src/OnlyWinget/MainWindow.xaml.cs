using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OnlyWinget.Application.App;
using OnlyWinget.Pages;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace OnlyWinget;

public sealed partial class MainWindow : Window
{
    private const double InitialWidth = 1180;
    private const double InitialHeight = 760;

    private readonly Dictionary<string, Type> pages = new(StringComparer.Ordinal)
    {
        ["dashboard"] = typeof(DashboardPage),
        ["presets"] = typeof(PresetsPage),
        ["search"] = typeof(SearchPage),
        ["updates"] = typeof(UpdatesPage),
        ["windowsUpdates"] = typeof(WindowsUpdatePage),
        ["sources"] = typeof(SourcesPage),
        ["activity"] = typeof(ActivityPage)
    };

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ResizeWindow();
        ApplyWindowIcon();
        RootNavigation.Loaded += OnLoaded;
        ApplyText();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    private void ResizeWindow()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(windowHandle) / 96d;
        AppWindow.Resize(new SizeInt32(
            (int)Math.Ceiling(InitialWidth * scale),
            (int)Math.Ceiling(InitialHeight * scale)));
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        RootNavigation.Loaded -= OnLoaded;
        try
        {
            await new ApplicationStartupOrchestrator(App.Workflow).InitializeAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.OnLoaded", exception);
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item ||
            item.Tag is not string tag ||
            !pages.TryGetValue(tag, out var pageType))
        {
            return;
        }

        ContentFrame.Navigate(pageType);
    }

    private void ApplyText()
    {
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            item.Content = item.Tag switch
            {
                "dashboard" => TextResources.Get("Nav_Dashboard"),
                "presets" => TextResources.Get("Nav_Presets"),
                "search" => TextResources.Get("Nav_Search"),
                "updates" => TextResources.Get("Nav_Updates"),
                "windowsUpdates" => TextResources.Get("Nav_WindowsUpdates"),
                "sources" => TextResources.Get("Nav_Sources"),
                "activity" => TextResources.Get("Nav_Activity"),
                _ => item.Content
            };
        }
    }

    private void ApplyWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "OnlyWinget.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
    }
}
