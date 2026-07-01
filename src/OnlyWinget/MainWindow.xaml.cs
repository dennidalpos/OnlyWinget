using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using OnlyWinget.Application.App;
using OnlyWinget.Shell;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace OnlyWinget;

public sealed partial class MainWindow : Window
{
    private const double InitialWidth = 1180;
    private const double InitialHeight = 760;

    private readonly IReadOnlyList<NavigationRoute> routeDefinitions = App.UiServices.Navigation.Routes;
    private readonly IReadOnlyDictionary<string, NavigationRoute> routes = App.UiServices.Navigation.Routes
        .ToDictionary(route => route.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, Page> pageCache = new(StringComparer.Ordinal);

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ResizeWindow();
        ApplyWindowIcon();
        App.UiServices.Settings.Changed += OnSettingsChanged;
        Closed += OnClosed;
        RootNavigation.Loaded += OnLoaded;
        ApplyTheme();
        BuildNavigation();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ShowPage("home");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        App.UiServices.Settings.Changed -= OnSettingsChanged;
        Closed -= OnClosed;
    }

    private void OnSettingsChanged(object? sender, EventArgs args)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            var selectedRoute = RootNavigation.SelectedItem is NavigationViewItem selected
                ? selected.Tag?.ToString()
                : null;
            selectedRoute ??= routeDefinitions.Single(route => route.IsSettings).Id;

            App.ApplySettings();
            ApplyTheme();
            pageCache.Clear();
            BuildNavigation();
            SelectRoute(selectedRoute);
            ShowPage(selectedRoute);
        });
    }

    private void ApplyTheme()
    {
        RootNavigation.RequestedTheme = App.UiServices.Settings.Current.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void SelectRoute(string routeId)
    {
        var route = routeDefinitions.Single(item => item.Id == routeId);
        if (route.IsSettings)
        {
            RootNavigation.SelectedItem = RootNavigation.SettingsItem;
            return;
        }

        RootNavigation.SelectedItem = RootNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .Single(item => string.Equals(item.Tag?.ToString(), routeId, StringComparison.Ordinal));
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
        var tag = args.IsSettingsSelected
            ? routeDefinitions.Single(route => route.IsSettings).Id
            : (args.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag is null || !routes.ContainsKey(tag))
        {
            return;
        }

        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        if (!pageCache.TryGetValue(tag, out var page))
        {
            page = routes[tag].CreatePage();
            pageCache[tag] = page;
        }

        if (!ReferenceEquals(PageHost.Content, page))
        {
            PageHost.Content = page;
        }
    }

    internal void Navigate(string routeId)
    {
        if (!routes.ContainsKey(routeId)) return;
        SelectRoute(routeId);
        ShowPage(routeId);
    }

    private void BuildNavigation()
    {
        RootNavigation.MenuItems.Clear();
        foreach (var route in routeDefinitions)
        {
            if (route.IsSettings)
            {
                if (RootNavigation.SettingsItem is NavigationViewItem settingsItem)
                {
                    settingsItem.Content = TextResources.Get(route.LabelResourceKey);
                    settingsItem.Tag = route.Id;
                    settingsItem.Icon = new SymbolIcon(route.Icon);
                }
                continue;
            }

            var item = new NavigationViewItem
            {
                Content = TextResources.Get(route.LabelResourceKey),
                Tag = route.Id,
                Icon = new SymbolIcon(route.Icon)
            };
            AutomationProperties.SetAutomationId(item, $"Nav{char.ToUpperInvariant(route.Id[0])}{route.Id[1..]}");
            RootNavigation.MenuItems.Add(item);
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
