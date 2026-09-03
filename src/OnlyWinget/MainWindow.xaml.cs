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

    // Mitigation for a reported "blank window" symptom after the app has been idle/backgrounded for a
    // long time — a known class of issue with WinUI3 Mica/Composition surfaces losing their DirectX
    // resources during long idle/suspend periods. Not a confirmed root cause (no repro captured yet);
    // this is a best-effort redraw nudge, not a guaranteed fix.
    private static readonly TimeSpan LongIdleThreshold = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<NavigationRoute> routeDefinitions = App.UiServices.Navigation.Routes;
    private readonly IReadOnlyDictionary<string, NavigationRoute> routes = App.UiServices.Navigation.Routes
        .ToDictionary(route => route.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, Page> pageCache = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource windowLifetime = new();
    private string? lastLanguage;
    private string? lastTheme;
    private string currentRouteId = "home";
    private bool isRestoringNavigation;
    private bool isNavigating;
    private DateTimeOffset lastActivatedAt = DateTimeOffset.UtcNow;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop();
        Activated += OnWindowActivated;
        ResizeAndCenterWindow();
        ApplyWindowIcon();
        if (Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += (_, _) => UpdateTitleBarButtons();
        }
        var currentSettings = App.UiServices.Settings.Current;
        lastLanguage = currentSettings.Language;
        lastTheme = currentSettings.Theme;
        RootNavigation.OpenPaneLength = Math.Clamp(currentSettings.SidebarWidth, 180, 400);
        App.UiServices.Settings.Changed += OnSettingsChanged;
        App.Workflow.StateChanged += OnWorkflowStateChanged;
        Closed += OnClosed;
        SizeChanged += OnWindowSizeChanged;
        AppWindow.Changed += OnAppWindowChanged;
        RootNavigation.Loaded += OnLoaded;
        UpdateTitleBarPadding();
        ApplyTheme();
        BuildNavigation();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ShowPage("home");
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnWindowActivated;
        SizeChanged -= OnWindowSizeChanged;
        AppWindow.Changed -= OnAppWindowChanged;
        App.UiServices.Settings.Changed -= OnSettingsChanged;
        App.Workflow.StateChanged -= OnWorkflowStateChanged;
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

            if (themeChanged)
            {
                lastTheme = currentSettings.Theme;
                ApplyTheme();
            }

            if (languageChanged)
            {
                lastLanguage = currentSettings.Language;

                var selectedRoute = RootNavigation.SelectedItem is NavigationViewItem selected
                    ? selected.Tag?.ToString()
                    : null;
                selectedRoute ??= routeDefinitions.Single(route => route.IsSettings).Id;

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
        UpdateTitleBarButtons();
    }

    private void UpdateTitleBarButtons()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported() || AppWindow?.TitleBar == null)
        {
            return;
        }

        var isDark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
        var titleBar = AppWindow.TitleBar;

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (isDark)
        {
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(140, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
        }
        else
        {
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(140, 0, 0, 0);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(40, 0, 0, 0);
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

    private void ResizeAndCenterWindow()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var scale = GetDpiForWindow(windowHandle) / 96d;

        var desiredWidth = (int)Math.Ceiling(InitialWidth * scale);
        var desiredHeight = (int)Math.Ceiling(InitialHeight * scale);

        var width = Math.Min(desiredWidth, workArea.Width);
        var height = Math.Min(desiredHeight, workArea.Height);

        var x = Math.Max(workArea.X, workArea.X + (workArea.Width - width) / 2);
        var y = Math.Max(workArea.Y, workArea.Y + (workArea.Height - height) / 2);

        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        UpdateTitleBarPadding();

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var idleFor = now - lastActivatedAt;
        lastActivatedAt = now;

        if (idleFor >= LongIdleThreshold)
        {
            RefreshAfterLongIdle(idleFor);
        }
    }

    private void RefreshAfterLongIdle(TimeSpan idleFor)
    {
        try
        {
            AppDiagnostics.Write($"Window reactivated after {idleFor:hh\\:mm\\:ss} idle; applying blank-window mitigation.");
            SystemBackdrop = null;
            if (Content is FrameworkElement rootElement)
            {
                rootElement.InvalidateMeasure();
                rootElement.InvalidateArrange();
            }

            DispatcherQueue.TryEnqueue(() => SystemBackdrop = new MicaBackdrop());
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.RefreshAfterLongIdle", exception);
        }
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        UpdateTitleBarPadding();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPositionChange)
        {
            UpdateTitleBarPadding();
        }
    }

    private void UpdateTitleBarPadding()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(windowHandle);
        var scale = dpi > 0 ? dpi / 96d : 1.0;

        var leftInsetDip = AppWindow.TitleBar.LeftInset / scale;
        var rightInsetDip = AppWindow.TitleBar.RightInset / scale;

        AppTitleBar.Padding = new Thickness(
            Math.Max(16, leftInsetDip),
            0,
            Math.Max(16, rightInsetDip),
            0);
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        RootNavigation.Loaded -= OnLoaded;
        UpdateTitleBarPadding();
        try
        {
            await AppComposition.CreateStartupOrchestrator().InitializeAsync(windowLifetime.Token);
            UpdateStatusBadges();
        }
        catch (OperationCanceledException) when (windowLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.OnLoaded", exception);
        }
    }

    private void OnWorkflowStateChanged(object? sender, EventArgs args)
    {
        _ = DispatcherQueue.TryEnqueue(UpdateStatusBadges);
    }

    private void UpdateStatusBadges()
    {
        var caps = App.Workflow.State.Capabilities;

        if (caps.IsWingetAvailable == true)
        {
            var rawVersion = caps.WingetVersion?.TrimStart('v', 'V');
            WingetStatusBadge.Text = string.IsNullOrWhiteSpace(rawVersion) ? "Winget Available" : $"Winget v{rawVersion}";
            WingetStatusBadge.Glyph = "\uE802";
            WingetStatusBadge.Severity = Controls.BadgeSeverity.Success;
            WingetStatusBadge.Visibility = Visibility.Visible;
        }
        else if (caps.IsWingetAvailable == false)
        {
            WingetStatusBadge.Text = "Winget Missing";
            WingetStatusBadge.Glyph = "\uE711";
            WingetStatusBadge.Severity = Controls.BadgeSeverity.Error;
            WingetStatusBadge.Visibility = Visibility.Visible;
        }
    }

    private async void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (isRestoringNavigation || isNavigating)
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

        isNavigating = true;
        try
        {
            if (!await ConfirmCurrentNavigationAsync())
            {
                isRestoringNavigation = true;
                SelectRoute(currentRouteId);
                isRestoringNavigation = false;
                return;
            }

            ShowPage(tag);
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.OnSelectionChanged", exception);
        }
        finally
        {
            isNavigating = false;
        }
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

    private async Task<bool> ConfirmCurrentNavigationAsync()
    {
        try
        {
            return PageHost.Content is IPendingNavigationGuard guard
                ? await guard.ConfirmNavigationAsync()
                : true;
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("MainWindow.ConfirmCurrentNavigationAsync", exception);
            return true;
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

    private async void OnOpenLogsTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        try
        {
            var dialog = new Controls.LogViewerDialog();
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppDiagnostics.WriteException("MainWindow.OnOpenLogsTapped", ex);
            AppDiagnostics.OpenLog();
        }
    }
}
