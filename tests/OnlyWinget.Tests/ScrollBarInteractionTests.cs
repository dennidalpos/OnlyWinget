using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;

namespace OnlyWinget.Tests;

[Collection(nameof(WpfUiCollection))]
public sealed class ScrollBarInteractionTests
{
    private static readonly object WpfHostLock = new();
    private static Thread? _wpfThread;
    private static Dispatcher? _wpfDispatcher;

    [Fact]
    public void UpdatesList_ScrollThumbsMoveWhenScrollViewerOffsetsChange()
    {
        var root = Path.Combine(Path.GetTempPath(), $"OnlyWinget-ScrollThumbs-{Guid.NewGuid():N}");
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

                    SetUpdates(viewModel, Enumerable.Range(1, 60).Select(index => new UpdateEntry
                    {
                        Name = $"Package {index:00} with an intentionally long descriptive label to force wide rows",
                        Id = $"Contoso.Product.Component.With.A.Long.Identifier.{index:00}",
                        Version = $"1.{index}.0",
                        Available = $"2.{index}.0",
                        Status = "Installa",
                        ErrorMessage = $"Error detail {index:00} with additional explanatory context",
                        Resolution = $"Resolution guidance {index:00} with additional explanatory context"
                    }).ToArray());

                    window.Show();
                    DoEvents();
                    window.UpdateLayout();
                    DoEvents();

                    var updatesList = Assert.IsType<ListView>(window.FindName("UpdatesList"));
                    var scrollViewer = FindDescendants<ScrollViewer>(updatesList)
                        .FirstOrDefault(candidate => candidate.ComputedVerticalScrollBarVisibility == Visibility.Visible
                                                     || candidate.ComputedHorizontalScrollBarVisibility == Visibility.Visible);

                    Assert.NotNull(scrollViewer);

                    Assert.True(scrollViewer!.ScrollableHeight > 0d, "Expected a vertical scrollable extent.");
                    Assert.True(scrollViewer.ScrollableWidth > 0d, "Expected a horizontal scrollable extent.");

                    var verticalScrollBar = FindVisibleScrollBar(updatesList, Orientation.Vertical);
                    var horizontalScrollBar = FindVisibleScrollBar(updatesList, Orientation.Horizontal);

                    var verticalStart = GetThumbLead(verticalScrollBar, Orientation.Vertical);
                    var horizontalStart = GetThumbLead(horizontalScrollBar, Orientation.Horizontal);

                    scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight);
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.ScrollableWidth);
                    scrollViewer.UpdateLayout();
                    updatesList.UpdateLayout();
                    DoEvents();

                    Assert.True(scrollViewer.VerticalOffset > 0d, "Expected the vertical offset to change.");
                    Assert.True(scrollViewer.HorizontalOffset > 0d, "Expected the horizontal offset to change.");

                    var verticalEnd = GetThumbLead(verticalScrollBar, Orientation.Vertical);
                    var horizontalEnd = GetThumbLead(horizontalScrollBar, Orientation.Horizontal);

                    var verticalTrack = Assert.IsType<Track>(verticalScrollBar.Template.FindName("PART_Track", verticalScrollBar));
                    var horizontalTrack = Assert.IsType<Track>(horizontalScrollBar.Template.FindName("PART_Track", horizontalScrollBar));
                    var verticalThumb = Assert.IsType<Thumb>(verticalTrack.Thumb);
                    var horizontalThumb = Assert.IsType<Thumb>(horizontalTrack.Thumb);

                    Assert.True(
                        verticalEnd > verticalStart + 4d,
                        $"Vertical thumb did not move. Start: {verticalStart}, end: {verticalEnd}, scrollViewerOffset: {scrollViewer.VerticalOffset}, scrollBarValue: {verticalScrollBar.Value}, trackValue: {verticalTrack.Value}, thumbHeight: {verticalThumb.ActualHeight}, trackHeight: {verticalTrack.ActualHeight}.");
                    Assert.True(
                        horizontalEnd > horizontalStart + 4d,
                        $"Horizontal thumb did not move. Start: {horizontalStart}, end: {horizontalEnd}, scrollViewerOffset: {scrollViewer.HorizontalOffset}, scrollBarValue: {horizontalScrollBar.Value}, trackValue: {horizontalTrack.Value}, thumbWidth: {horizontalThumb.ActualWidth}, trackWidth: {horizontalTrack.ActualWidth}.");
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

    private static void SetUpdates(MainViewModel viewModel, params UpdateEntry[] updates)
    {
        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.Updates));
        var setter = property?.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(viewModel, new object[] { new ObservableCollection<UpdateEntry>(updates) });
    }

    private static ScrollBar FindVisibleScrollBar(DependencyObject root, Orientation orientation)
    {
        var scrollBar = FindDescendants<ScrollBar>(root)
            .FirstOrDefault(candidate => candidate.Orientation == orientation
                                         && candidate.Visibility == Visibility.Visible
                                         && candidate.ActualWidth > 0d
                                         && candidate.ActualHeight > 0d);

        return Assert.IsType<ScrollBar>(scrollBar);
    }

    private static double GetThumbLead(ScrollBar scrollBar, Orientation orientation)
    {
        scrollBar.ApplyTemplate();
        var track = Assert.IsType<Track>(scrollBar.Template.FindName("PART_Track", scrollBar));
        track.ApplyTemplate();

        var thumb = Assert.IsType<Thumb>(track.Thumb);
        thumb.UpdateLayout();
        track.UpdateLayout();

        var bounds = thumb.TransformToAncestor(track)
            .TransformBounds(new Rect(new Point(0d, 0d), new Size(thumb.ActualWidth, thumb.ActualHeight)));

        return orientation == Orientation.Vertical ? bounds.Top : bounds.Left;
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
        var dataService = new AppDataService(appDataRoot: root, appBaseDirectory: root);
        var queryService = new WingetQueryService(wingetService);
        var localizationService = new LocalizationService(
            new AppPreferencesService(root),
            () => CultureInfo.GetCultureInfo("it-IT"));

        return new MainViewModel(
            queryService,
            new PresetWorkspaceService(dataService),
            localizationService,
            new FakeDialogService(),
            new AppEntryService(wingetService),
            new TabService(),
            operationRunner,
            new UpdatesWorkspaceService(queryService, operationRunner));
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
            if (Application.Current?.Dispatcher is Dispatcher currentDispatcher
                && !currentDispatcher.HasShutdownStarted
                && !currentDispatcher.HasShutdownFinished)
            {
                _wpfDispatcher = currentDispatcher;
                return;
            }

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
            Action<string, string, string>? setErrorById = null)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null)
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
