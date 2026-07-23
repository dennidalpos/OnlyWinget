using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using OnlyWinget.Application.App;
using OnlyWinget.Presentation;
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
    private readonly CancellationTokenSource windowLifetime = new();
    private string? lastLanguage;
    private string? lastTheme;
    private string currentRouteId = "home";
    private bool isRestoringNavigation;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        ResizeWindow();
        ApplyWindowIcon();
        var currentSettings = App.UiServices.Settings.Current;
        lastLanguage = currentSettings.Language;
        lastTheme = currentSettings.Theme;
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
        windowLifetime.Cancel();
        windowLifetime.Dispose();
    }

    private void OnSettingsChanged(object? sender, EventArgs args)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            var currentSettings = App.UiServices.Settings.Current;
            var languageChanged = lastLanguage != currentSettings.Language;
            var themeChanged = lastTheme != currentSettings.Theme;

            App.ApplySettings();

            if (languageChanged || themeChanged)
            {
                lastLanguage = currentSettings.Language;
                lastTheme = currentSettings.Theme;

                var selectedRoute = RootNavigation.SelectedItem is NavigationViewItem selected
                    ? selected.Tag?.ToString()
                    : null;
                selectedRoute ??= routeDefinitions.Single(route => route.IsSettings).Id;

                ApplyTheme();
                pageCache.Clear();
                BuildNavigation();
                SelectRoute(selectedRoute);
                ShowPage(selectedRoute);
            }
        });
    }

    private void ApplyTheme()
    {
        var theme = App.UiServices.Settings.Current.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        RootNavigation.RequestedTheme = theme;
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }
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
            await new ApplicationStartupOrchestrator(App.Workflow).InitializeAsync(windowLifetime.Token);
        }
        catch (OperationCanceledException) when (windowLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.OnLoaded", exception);
        }
    }

    private async void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (isRestoringNavigation)
        {
            return;
        }

        var tag = args.IsSettingsSelected
            ? routeDefinitions.Single(route => route.IsSettings).Id
            : (args.SelectedItem as NavigationViewItem)?.Tag as string;
        if (tag is null || !routes.ContainsKey(tag))
        {
            return;
        }

        if (!await ConfirmCurrentNavigationAsync())
        {
            isRestoringNavigation = true;
            SelectRoute(currentRouteId);
            isRestoringNavigation = false;
            return;
        }

        ShowPage(tag);
    }

    private async void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.IsSettingsInvoked
            ? routeDefinitions.Single(route => route.IsSettings).Id
            : (args.InvokedItemContainer as NavigationViewItem)?.Tag as string;
        if (tag is not null && routes.ContainsKey(tag) && await ConfirmCurrentNavigationAsync()) ShowPage(tag);
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

        currentRouteId = tag;
    }

    private async Task<bool> ConfirmCurrentNavigationAsync() =>
        PageHost.Content is IPendingNavigationGuard guard
            ? await guard.ConfirmNavigationAsync()
            : true;

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
            var brushKey = route.Id switch
            {
                "home" => "AreaHomeBrush",
                "packages" => "AreaPackagesBrush",
                "updates" => "AreaUpdatesBrush",
                "sources" => "AreaSourcesBrush",
                "activity" => "AreaActivityBrush",
                "settings" => "AreaSettingsBrush",
                _ => null
            };

            Brush? brush = null;
            if (brushKey is not null && Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(brushKey, out var res) && res is Brush b)
            {
                brush = b;
            }

            if (route.IsSettings)
            {
                if (RootNavigation.SettingsItem is NavigationViewItem settingsItem)
                {
                    settingsItem.Content = TextResources.Get(route.LabelResourceKey);
                    settingsItem.Tag = route.Id;
                    settingsItem.Icon = new SymbolIcon(route.Icon);
                    if (brush is not null)
                    {
                        settingsItem.Resources["NavigationViewSelectionIndicatorForeground"] = brush;
                    }
                }
                continue;
            }

            var item = new NavigationViewItem
            {
                Content = TextResources.Get(route.LabelResourceKey),
                Tag = route.Id,
                Icon = new SymbolIcon(route.Icon)
            };
            if (brush is not null)
            {
                item.Resources["NavigationViewSelectionIndicatorForeground"] = brush;
            }
            AutomationProperties.SetAutomationId(item, $"Nav{char.ToUpperInvariant(route.Id[0])}{route.Id[1..]}");
            RootNavigation.MenuItems.Add(item);
        }

        if (OpenLogsItem is NavigationViewItem openLogsNav)
        {
            openLogsNav.Content = TextResources.Get("Menu_OpenLogs");
            ToolTipService.SetToolTip(openLogsNav, TextResources.Get("Menu_OpenLogs"));
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

    private void OnOpenLogsTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        AppDiagnostics.OpenLog();
    }
}
