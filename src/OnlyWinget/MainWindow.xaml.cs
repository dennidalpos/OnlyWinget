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
        ApplyMenuSettings();
        typeof(Microsoft.UI.Xaml.UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(PaneResizer, Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast));
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
            ApplyMenuSettings();

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
        UpdatePageHostSize();
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

    private void ApplyMenuSettings()
    {
        var settings = App.UiServices.Settings.Current;

        // Apply pin state
        if (settings.IsMenuPinned)
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            RootNavigation.IsPaneOpen = true;
            RootNavigation.CompactModeThresholdWidth = 0;
            RootNavigation.ExpandedModeThresholdWidth = 0;

            // Ensure width is sufficient to completely display menu text (at least 260)
            double sufficientWidth = Math.Max(260, settings.MenuWidth > 0 ? settings.MenuWidth : 280);
            RootNavigation.OpenPaneLength = sufficientWidth;
        }
        else
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
            RootNavigation.CompactModeThresholdWidth = 0; // Always keep menu icons visible on small screen sizes
            RootNavigation.ExpandedModeThresholdWidth = 1100;

            // Determine if the pane should be open based on window width
            var windowHandle = WindowNative.GetWindowHandle(this);
            var scale = GetDpiForWindow(windowHandle) / 96d;
            var windowWidth = AppWindow.Size.Width / scale;
            RootNavigation.IsPaneOpen = windowWidth >= 1100;

            RootNavigation.OpenPaneLength = settings.MenuWidth > 0 ? settings.MenuWidth : 280;
        }

        // Update pin item content & icon
        if (PinMenuItem is not null)
        {
            PinMenuItem.Content = TextResources.Get(settings.IsMenuPinned ? "Menu_Unpin" : "Menu_Pin");
            if (PinMenuIcon is FontIcon fontIcon)
            {
                fontIcon.Glyph = settings.IsMenuPinned ? "\uE840" : "\uE718";
            }
        }

        if (OpenLogsItem is not null)
        {
            OpenLogsItem.Content = TextResources.Get("Menu_OpenLogs");
        }

        UpdateResizer();
    }

    private void UpdateResizer()
    {
        if (RootNavigation == null || PaneResizer == null) return;

        var settings = App.UiServices.Settings.Current;
        bool isPaneOpen = RootNavigation.IsPaneOpen;
        bool isLeftMode = RootNavigation.PaneDisplayMode != NavigationViewPaneDisplayMode.Top;
        bool isPinned = settings.IsMenuPinned;

        if (isPaneOpen && isLeftMode && !isPinned)
        {
            PaneResizer.Visibility = Visibility.Visible;
            PaneResizer.Margin = new Thickness(RootNavigation.OpenPaneLength - 4, 0, 0, 0);
        }
        else
        {
            PaneResizer.Visibility = Visibility.Collapsed;
        }
    }

    private Brush GetActiveSectionBrush()
    {
        string? routeId = (RootNavigation.SelectedItem as NavigationViewItem)?.Tag as string;
        routeId ??= "home";
        var brushKey = routeId switch
        {
            "home" => "AreaHomeBrush",
            "packages" => "AreaPackagesBrush",
            "updates" => "AreaUpdatesBrush",
            "sources" => "AreaSourcesBrush",
            "activity" => "AreaActivityBrush",
            "settings" => "AreaSettingsBrush",
            _ => "SystemControlForegroundAccentBrush"
        };
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(brushKey, out var res) && res is Brush b)
        {
            return b;
        }
        return new SolidColorBrush(Colors.DodgerBlue);
    }

    private void OnPaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
    {
        if (App.UiServices.Settings.Current.IsMenuPinned)
        {
            args.Cancel = true;
        }
    }

    private void OnPaneOpened(NavigationView sender, object args)
    {
        UpdateResizer();
    }

    private void OnPaneClosed(NavigationView sender, object args)
    {
        UpdateResizer();
    }

    private void OnDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        UpdateResizer();
    }

    private async void OnPinMenuTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var currentSettings = App.UiServices.Settings.Current;
        var newPinState = !currentSettings.IsMenuPinned;
        var updatedSettings = currentSettings with { IsMenuPinned = newPinState };
        await SaveSettingsAsync(updatedSettings, "MainWindow.OnPinMenuTapped");
    }

    private void OnOpenLogsTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        AppDiagnostics.OpenLog();
    }

    private bool isDraggingResizer;
    private double dragStartPaneLength;
    private double dragStartPointerX;

    private void OnResizerPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var border = (Border)sender;
        var properties = e.GetCurrentPoint(border).Properties;
        if (properties.IsLeftButtonPressed)
        {
            isDraggingResizer = true;
            border.CapturePointer(e.Pointer);
            dragStartPaneLength = RootNavigation.OpenPaneLength;
            var point = e.GetCurrentPoint(RootNavigation);
            dragStartPointerX = point.Position.X;
            ResizerVisualLine.Opacity = 1.0;
            e.Handled = true;
        }
    }

    private void OnResizerPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (isDraggingResizer)
        {
            var point = e.GetCurrentPoint(RootNavigation);
            var deltaX = point.Position.X - dragStartPointerX;
            var newLength = dragStartPaneLength + deltaX;
            newLength = Math.Max(150, Math.Min(500, newLength));
            RootNavigation.OpenPaneLength = newLength;
            UpdateResizer();
            e.Handled = true;
        }
    }

    private async void OnResizerPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (isDraggingResizer)
        {
            isDraggingResizer = false;
            var border = (Border)sender;
            border.ReleasePointerCapture(e.Pointer);
            var point = e.GetCurrentPoint(border);
            var isOver = point.Position.X >= 0 && point.Position.X <= border.ActualWidth &&
                         point.Position.Y >= 0 && point.Position.Y <= border.ActualHeight;
            if (!isOver)
            {
                ResizerVisualLine.Opacity = 0.0;
            }
            var currentSettings = App.UiServices.Settings.Current;
            var updatedSettings = currentSettings with { MenuWidth = RootNavigation.OpenPaneLength };
            await SaveSettingsAsync(updatedSettings, "MainWindow.OnResizerPointerReleased");
            e.Handled = true;
        }
    }

    private async Task SaveSettingsAsync(Services.AppSettings settings, string caller)
    {
        try
        {
            await App.UiServices.Settings.SaveAsync(settings, windowLifetime.Token);
        }
        catch (OperationCanceledException) when (windowLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException(caller, exception);
        }
    }

    private void OnResizerPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ResizerVisualLine.Background = GetActiveSectionBrush();
        ResizerVisualLine.Opacity = 1.0;
    }

    private void OnResizerPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!isDraggingResizer)
        {
            ResizerVisualLine.Opacity = 0.0;
        }
    }

    private void OnMainPageScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageHostSize();
    }

    private void UpdatePageHostSize()
    {
        if (MainPageScrollViewer is ScrollViewer sv)
        {
            double minWidth = 720;
            double minHeight = 680;

            if (PageHost.Content is FrameworkElement fe)
            {
                if (fe.MinWidth > 0) minWidth = fe.MinWidth;
                if (fe.MinHeight > 0) minHeight = fe.MinHeight;

                var pageTypeName = fe.GetType().Name;
                if (pageTypeName is "DashboardPage" or "SettingsPage")
                {
                    PageHost.Height = double.NaN;
                    PageHost.Width = Math.Max(minWidth, sv.ViewportWidth);
                    return;
                }
            }

            PageHost.Width = Math.Max(minWidth, sv.ViewportWidth);
            PageHost.Height = Math.Max(minHeight, sv.ViewportHeight);
        }
    }
}
