using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;

namespace OnlyWinget.Tests;

[CollectionDefinition(nameof(WpfUiCollection), DisableParallelization = true)]
public sealed class WpfUiCollection
{
}

[Collection(nameof(WpfUiCollection))]
public sealed class SearchResultsLayoutTests
{
    private static readonly object WpfHostLock = new();
    private static Thread? _wpfThread;
    private static Dispatcher? _wpfDispatcher;

    [Theory]
    [InlineData("preset", 860, 640)]
    [InlineData("search", 1180, 720)]
    [InlineData("updates", 1567, 1050)]
    public void MainShell_RendersCoreWorkspacesInsideWindowBounds(string workspace, double width, double height)
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-ShellBounds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = width,
                    Height = height,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();
                    SeedPresetRows(viewModel);
                    ApplyWorkspace(viewModel, workspace);

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    AssertElementVisibleInsideWindow(window, "OutputLogBox");

                    if (workspace == "preset")
                    {
                        AssertElementVisibleInsideWindow(window, "PresetAppsList");
                    }
                    else if (workspace == "search")
                    {
                        AssertElementVisibleInsideWindow(window, "SearchResultsList");
                    }
                    else
                    {
                        AssertElementVisibleInsideWindow(window, "UpdatesList");
                    }
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MainShell_MaximizedWindowFitsInsideDesktopWorkArea()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-MaximizedBounds-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var workArea = SystemParameters.WorkArea;
                var window = new MainWindow
                {
                    Width = 1180,
                    Height = 720,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = workArea.Left,
                    Top = workArea.Top,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();
                    SeedPresetRows(viewModel);

                    window.Show();
                    DoEvents();

                    window.WindowState = WindowState.Maximized;
                    window.UpdateLayout();
                    DoEvents();

                    Assert.Equal(WindowState.Maximized, window.WindowState);
                    Assert.True(window.ActualWidth <= workArea.Width + 1d, $"Maximized width should fit work area. Actual: {window.ActualWidth}, work area: {workArea.Width}.");
                    Assert.True(window.ActualHeight <= workArea.Height + 1d, $"Maximized height should fit work area. Actual: {window.ActualHeight}, work area: {workArea.Height}.");
                    AssertElementVisibleInsideWindow(window, "OutputLogBox");
                    AssertElementVisibleInsideWindow(window, "PresetAppsList");
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("preset")]
    [InlineData("search")]
    [InlineData("updates")]
    public void MainShell_VisibleInteractiveControlsExposeKeyboardAndAccessibleState(string workspace)
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-A11y-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = 1366,
                    Height = 900,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();
                    SeedPresetRows(viewModel);
                    ApplyWorkspace(viewModel, workspace);

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    var interactiveControls = FindDescendants<Control>(window)
                        .Where(IsUserInteractiveControl)
                        .Where(control => control.IsVisible && control.IsEnabled)
                        .ToList();

                    Assert.NotEmpty(interactiveControls);

                    foreach (var control in interactiveControls)
                    {
                        Assert.True(control.Focusable, $"{control.GetType().Name} should be keyboard focusable.");

                        if (RequiresAccessibleName(control))
                        {
                            Assert.False(
                                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                                $"{control.GetType().Name} should expose an automation name when visible.");
                        }
                    }
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SearchResultsLayout_RendersRegressionSamplesWithoutTruncationAtReportedWidth()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-Layout-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = 1567,
                    Height = 1050,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();

                    window.Show();
                    DoEvents();

                    viewModel.OpenSearchCommand.Execute(null);
                    SetSearchResults(
                        viewModel,
                        new SearchResult
                        {
                            Name = "Microsoft .NET Windows Desktop Runtime 10.0",
                            Id = "Microsoft.DotNet.DesktopRuntime.10",
                            Version = "10.0.6"
                        },
                        new SearchResult
                        {
                            Name = "Microsoft ASP.NET Core Runtime 11.0 Preview",
                            Id = "Microsoft.DotNet.AspNetCore.Preview",
                            Version = "11.0.0-preview.7"
                        });

                    window.UpdateLayout();
                    DoEvents();

                    var searchResultsList = Assert.IsType<ListView>(window.FindName("SearchResultsList"));
                    searchResultsList.ScrollIntoView(viewModel.SearchResults[0]);
                    searchResultsList.UpdateLayout();
                    DoEvents();

                    var gridView = Assert.IsType<GridView>(searchResultsList.View);
                    Assert.True(gridView.Columns[0].Width >= 320d);
                    Assert.True(gridView.Columns[1].Width >= 360d);
                    Assert.True(gridView.Columns[2].Width >= 180d);

                    AssertTextFitsSingleLine(searchResultsList, "Microsoft .NET Windows Desktop Runtime 10.0");
                    AssertTextFitsSingleLine(searchResultsList, "Microsoft.DotNet.DesktopRuntime.10");
                    AssertTextFitsSingleLine(searchResultsList, "11.0.0-preview.7");
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UpdatesList_ScrollBarsRenderUsableThumbsAndTrackBindings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-Scrollbars-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = 1180,
                    Height = 720,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();
                    viewModel.IsUpdatesVisible = true;

                    SetUpdates(viewModel, Enumerable.Range(1, 40).Select(index => new UpdateEntry
                    {
                        Name = $"Package {index:00} with a long descriptive label",
                        Id = $"Contoso.Product.Component.{index:00}",
                        Version = $"1.{index}.0",
                        Available = $"2.{index}.0",
                        Status = "Installa",
                        ErrorMessage = $"Error detail {index:00}",
                        Resolution = $"Resolution guidance {index:00}"
                    }).ToArray());

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    var updatesList = Assert.IsType<ListView>(window.FindName("UpdatesList"));
                    updatesList.ScrollIntoView(viewModel.Updates[^1]);
                    updatesList.UpdateLayout();
                    DoEvents();

                    AssertScrollBar(updatesList, Orientation.Vertical, minimumThumbLength: 18d);
                    AssertScrollBar(updatesList, Orientation.Horizontal, minimumThumbLength: 18d);
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UpdatesList_UsesCompactStatusColumnAndReadableErrorResolutionSizing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-UpdatesGrid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = 1567,
                    Height = 1050,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();
                    viewModel.IsUpdatesVisible = true;

                    SetUpdates(
                        viewModel,
                        new UpdateEntry
                        {
                            Name = "Microsoft .NET SDK",
                            Id = "Microsoft.DotNet.SDK.9",
                            Version = "9.0.100",
                            Available = "9.0.200",
                            Source = "winget",
                            Status = "Installa",
                            ErrorMessage = "Package returned a recoverable update error",
                            Resolution = "Review package options and retry the update"
                        });

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    var updatesList = Assert.IsType<ListView>(window.FindName("UpdatesList"));
                    var gridView = Assert.IsType<GridView>(updatesList.View);

                    Assert.Equal(8, gridView.Columns.Count);
                    Assert.Equal(44d, gridView.Columns[0].Width);
                    Assert.Equal(54d, gridView.Columns[1].Width);
                    Assert.True(gridView.Columns[2].Width >= 220d);
                    Assert.True(gridView.Columns[3].Width >= 280d);
                    Assert.True(gridView.Columns[6].Width >= 180d);
                    Assert.True(gridView.Columns[7].Width >= 220d);

                    var selectUpdateCheckBox = FindDescendants<CheckBox>(updatesList).FirstOrDefault();
                    Assert.NotNull(selectUpdateCheckBox);
                    AssertElementVisibleInsideWindow(window, selectUpdateCheckBox!);

                    var statusBadge = FindDescendants<Border>(updatesList)
                        .FirstOrDefault(candidate => AutomationProperties.GetName(candidate) == "Installa");
                    Assert.NotNull(statusBadge);
                    Assert.Equal("Installa", statusBadge!.ToolTip);

                    Assert.Contains(FindDescendants<TextBlock>(updatesList), textBlock => textBlock.Text == "\uE946");
                    Assert.DoesNotContain(FindDescendants<TextBlock>(updatesList), textBlock => textBlock.Text == "Installa");

                    var horizontalScrollViewer = FindDescendants<ScrollViewer>(updatesList)
                        .FirstOrDefault(candidate => candidate.ComputedHorizontalScrollBarVisibility == Visibility.Visible);

                    Assert.Null(horizontalScrollViewer);
                    AssertTextFitsSingleLine(updatesList, "Come risolvere");
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PresetAppsList_KeepsAllColumnsVisibleAtDefaultWindowWidth()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-PresetGrid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            RunOnStaThread(() =>
            {
                EnsureApplicationResourcesLoaded();

                var window = new MainWindow
                {
                    Width = 1366,
                    Height = 768,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000,
                    Top = 0,
                    ShowInTaskbar = false
                };

                try
                {
                    var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner());
                    window.DataContext = viewModel;
                    viewModel.Initialize();

                    viewModel.PresetWorkspace.CurrentApps.Clear();
                    viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
                    {
                        Name = "WiX Toolset Command-Line Tools",
                        Id = "WiXToolset.WiXCLI",
                        Architecture = "x64",
                        Action = AppActions.Install,
                        Status = "Pronto",
                        ErrorMessage = "Errore",
                        Resolution = "Come risolvere"
                    });

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    var presetAppsList = Assert.IsType<ListView>(window.FindName("PresetAppsList"));
                    var gridView = Assert.IsType<GridView>(presetAppsList.View);

                    Assert.Equal(8, gridView.Columns.Count);
                    Assert.Equal(44d, gridView.Columns[0].Width);
                    Assert.Equal(54d, gridView.Columns[1].Width);
                    Assert.True(gridView.Columns[2].Width >= 180d);
                    Assert.True(gridView.Columns[3].Width >= 220d);
                    Assert.True(gridView.Columns[4].Width >= 100d);
                    Assert.Equal(138d, gridView.Columns[5].Width);
                    Assert.True(gridView.Columns[6].Width >= 140d);
                    Assert.True(gridView.Columns[7].Width >= 160d);

                    var horizontalScrollViewer = FindDescendants<ScrollViewer>(presetAppsList)
                        .FirstOrDefault(candidate => candidate.ComputedHorizontalScrollBarVisibility == Visibility.Visible);

                    Assert.Null(horizontalScrollViewer);
                    AssertTextFitsSingleLine(presetAppsList, "Come risolvere");
                }
                finally
                {
                    window.Close();
                    DoEvents();
                }
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertTextFitsSingleLine(DependencyObject root, string expectedText)
    {
        var textBlock = FindDescendants<TextBlock>(root)
            .FirstOrDefault(candidate => string.Equals(candidate.Text, expectedText, StringComparison.Ordinal));

        Assert.NotNull(textBlock);
        Assert.True(textBlock!.ActualWidth > 0d);

        var pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;
        var formattedText = new FormattedText(
            textBlock.Text,
            CultureInfo.CurrentUICulture,
            textBlock.FlowDirection,
            new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
            textBlock.FontSize,
            Brushes.Black,
            pixelsPerDip);

        Assert.True(
            textBlock.ActualWidth + 4d >= formattedText.WidthIncludingTrailingWhitespace,
            $"Text '{expectedText}' is still narrower than the rendered content area.");
    }

    private static void AssertElementVisibleInsideWindow(Window window, string elementName)
    {
        var element = Assert.IsAssignableFrom<FrameworkElement>(window.FindName(elementName));
        AssertElementVisibleInsideWindow(window, element);
    }

    private static void AssertElementVisibleInsideWindow(Window window, FrameworkElement element)
    {
        var elementName = string.IsNullOrWhiteSpace(element.Name) ? element.GetType().Name : element.Name;
        Assert.True(element.IsVisible, $"{elementName} should be visible.");
        Assert.True(element.ActualWidth > 0d, $"{elementName} should have width.");
        Assert.True(element.ActualHeight > 0d, $"{elementName} should have height.");

        var bounds = element.TransformToAncestor(window)
            .TransformBounds(new Rect(new Point(0d, 0d), new Size(element.ActualWidth, element.ActualHeight)));

        Assert.True(bounds.Left >= -1d, $"{elementName} should not overflow left. Left: {bounds.Left}.");
        Assert.True(bounds.Top >= -1d, $"{elementName} should not overflow top. Top: {bounds.Top}.");
        Assert.True(bounds.Right <= window.ActualWidth + 1d, $"{elementName} should not overflow right. Right: {bounds.Right}, window: {window.ActualWidth}.");
        Assert.True(bounds.Bottom <= window.ActualHeight + 1d, $"{elementName} should not overflow bottom. Bottom: {bounds.Bottom}, window: {window.ActualHeight}.");
    }

    private static bool IsUserInteractiveControl(Control control)
    {
        return control is Button
            or TextBox
            or ComboBox
            or ListBox
            or ListView
            or CheckBox;
    }

    private static bool RequiresAccessibleName(Control control)
    {
        if (control is TextBox or ComboBox or ListBox or ListView or CheckBox)
        {
            return true;
        }

        if (control is Button button)
        {
            return !HasVisibleTextContent(button);
        }

        return false;
    }

    private static bool HasVisibleTextContent(Button button)
    {
        return button.Content is string text
            && !string.IsNullOrWhiteSpace(text);
    }

    private static void SeedPresetRows(MainViewModel viewModel)
    {
        viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
        {
            Name = "Microsoft .NET SDK 9.0",
            Id = "Microsoft.DotNet.SDK.9",
            Source = "winget",
            Action = AppActions.Install,
            Architecture = "x64",
            Status = "Ready"
        });
    }

    private static void ApplyWorkspace(MainViewModel viewModel, string workspace)
    {
        switch (workspace)
        {
            case "preset":
                return;
            case "search":
                viewModel.OpenSearchCommand.Execute(null);
                SetSearchResults(
                    viewModel,
                    new SearchResult
                    {
                        Name = "Microsoft .NET Windows Desktop Runtime 10.0",
                        Id = "Microsoft.DotNet.DesktopRuntime.10",
                        Version = "10.0.6",
                        Source = "winget"
                    });
                return;
            case "updates":
                viewModel.IsUpdatesVisible = true;
                SetUpdates(
                    viewModel,
                    new UpdateEntry
                    {
                        Name = "Microsoft .NET SDK",
                        Id = "Microsoft.DotNet.SDK.9",
                        Version = "9.0.100",
                        Available = "9.0.200",
                        Source = "winget",
                        Status = "Ready"
                    });
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(workspace), workspace, "Unknown workspace.");
        }
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var index = 0; index < count; index++)
            {
                var child = VisualTreeHelper.GetChild(current, index);
                if (child is T match)
                {
                    yield return match;
                }

                queue.Enqueue(child);
            }
        }
    }

    private static void SetSearchResults(MainViewModel viewModel, params SearchResult[] results)
    {
        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.SearchResults), BindingFlags.Instance | BindingFlags.Public);
        var setter = property?.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(viewModel, new object[] { new ObservableCollection<SearchResult>(results) });
    }

    private static void SetUpdates(MainViewModel viewModel, params UpdateEntry[] updates)
    {
        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.Updates), BindingFlags.Instance | BindingFlags.Public);
        var setter = property?.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(viewModel, new object[] { new ObservableCollection<UpdateEntry>(updates) });
    }

    private static void AssertScrollBar(DependencyObject root, Orientation orientation, double minimumThumbLength)
    {
        var scrollBar = FindDescendants<ScrollBar>(root)
            .FirstOrDefault(candidate => candidate.Orientation == orientation && candidate.Visibility == Visibility.Visible);

        Assert.NotNull(scrollBar);
        Assert.True(scrollBar!.Maximum > 0d, $"{orientation} scrollbar maximum should reflect a scrollable extent.");
        Assert.True(scrollBar.ViewportSize > 0d, $"{orientation} scrollbar viewport should be populated.");

        var track = Assert.IsType<Track>(scrollBar.Template.FindName("PART_Track", scrollBar));
        Assert.Equal(scrollBar.Minimum, track.Minimum);
        Assert.Equal(scrollBar.Maximum, track.Maximum);
        Assert.Equal(scrollBar.ViewportSize, track.ViewportSize);

        var thumb = Assert.IsType<Thumb>(track.Thumb);
        var thumbLength = orientation == Orientation.Vertical ? thumb.ActualHeight : thumb.ActualWidth;
        Assert.True(
            thumbLength >= minimumThumbLength,
            $"{orientation} thumb is too small to use. Actual length: {thumbLength}.");
    }

    private static void DoEvents()
    {
        Application.Current?.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    private static void EnsureApplicationResourcesLoaded()
    {
        var app = Application.Current ?? throw new InvalidOperationException("WPF test host not initialized.");

        if (app.Resources.MergedDictionaries.Count > 0)
        {
            return;
        }

        var themeDictionary = (ResourceDictionary)Application.LoadComponent(
            new Uri("/OnlyWinget;component/Styles/Theme.xaml", UriKind.Relative));
        app.Resources.MergedDictionaries.Add(themeDictionary);
    }

    private static MainViewModel CreateViewModel(string root, WingetService wingetService, IOperationRunner operationRunner)
    {
        var dataService = new AppDataService(appDataRoot: root);
        var localizationService = new LocalizationService(
            new AppPreferencesService(root),
            () => CultureInfo.GetCultureInfo("it-IT"));

        return new MainViewModel(
            wingetService,
            dataService,
            localizationService,
            new FakeDialogService(),
            new AppEntryService(wingetService),
            new TabService(),
            operationRunner);
    }

    private static WingetService CreateWingetService()
    {
        return new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
    }

    private static void RunOnStaThread(Action action)
    {
        EnsureWpfHost();
        Exception? exception = null;
        _wpfDispatcher!.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }, DispatcherPriority.Send);

        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private static void EnsureWpfHost()
    {
        lock (WpfHostLock)
        {
            if (_wpfDispatcher != null && !_wpfDispatcher.HasShutdownStarted && !_wpfDispatcher.HasShutdownFinished)
            {
                return;
            }

            using var ready = new ManualResetEventSlim();
            _wpfThread = new Thread(() =>
            {
                _ = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                _wpfDispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });

            _wpfThread.SetApartmentState(ApartmentState.STA);
            _wpfThread.IsBackground = true;
            _wpfThread.Start();
            ready.Wait();
        }
    }

    private sealed class PassiveOperationRunner : IOperationRunner
    {
        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        public string Prompt(string prompt, string title, string defaultValue = "")
        {
            return defaultValue;
        }

        public void ShowInfo(string message, string title)
        {
        }

        public void ShowWarning(string message, string title)
        {
        }

        public void ShowError(string message, string title)
        {
        }

        public bool Confirm(string message, string title)
        {
            return false;
        }

        public string? OpenFile(string title, string filter, string defaultExtension = "json")
        {
            return null;
        }

        public string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json")
        {
            return null;
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request)
        {
            return Task.FromResult<PackageInterrogationDialogResult?>(null);
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry)
        {
            return Task.FromResult<PackageInterrogationDialogResult?>(null);
        }
    }
}
