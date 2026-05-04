// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Interop;
using OnlyWinget.Models;
using OnlyWinget.ViewModels;

namespace OnlyWinget;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateWindowStatePresentation();
        SourceInitialized += OnSourceInitialized;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowMessageHook);
    }

    private static IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMonitorWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.Monitor;

        minMaxInfo.MaxPosition.X = workArea.Left - monitorArea.Left;
        minMaxInfo.MaxPosition.Y = workArea.Top - monitorArea.Top;
        minMaxInfo.MaxSize.X = workArea.Right - workArea.Left;
        minMaxInfo.MaxSize.Y = workArea.Bottom - workArea.Top;

        Marshal.StructureToPtr(minMaxInfo, lParam, fDeleteOld: true);
    }

    private void OnSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (sender is not ListView listView)
        {
            return;
        }

        viewModel.SelectedSearchResults.Clear();
        foreach (var item in listView.SelectedItems)
        {
            if (item is Models.SearchResult result)
            {
                viewModel.SelectedSearchResults.Add(result);
            }
        }
    }

    private void OnListViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (sender is not ListView listView)
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (var item in listView.SelectedItems)
        {
            var line = item switch
            {
                UpdateEntry u => FormatRowText(u.Name, u.Id, u.Version, u.Available, u.Status, u.ErrorMessage, u.Resolution),
                AppEntry a => FormatRowText(a.Name, a.Id, a.Action, a.Status, a.ErrorMessage, a.Resolution),
                Models.SearchResult s => FormatRowText(s.Name, s.Id, s.Version),
                _ => null
            };

            if (line != null)
            {
                sb.AppendLine(line);
            }
        }

        if (sb.Length > 0)
        {
            Clipboard.SetText(sb.ToString().TrimEnd(Environment.NewLine.ToCharArray()));
            e.Handled = true;
        }
    }

    private static string FormatRowText(params string[] values)
    {
        return string.Join("\t", values);
    }

    private void OnOutputLogTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.CaretIndex = textBox.Text.Length;
            textBox.ScrollToEnd();
        }
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        UpdateWindowStatePresentation();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateWindowStatePresentation()
    {
        if (MaximizeRestoreGlyph == null || MaximizeRestoreButton == null)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeRestoreGlyph.Text = isMaximized ? "\uE923" : "\uE922";

        if (DataContext is MainViewModel viewModel)
        {
            AutomationProperties.SetName(MaximizeRestoreButton, isMaximized
                ? viewModel.Strings.RestoreWindowTooltip
                : viewModel.Strings.MaximizeWindowTooltip);
            MaximizeRestoreButton.ToolTip = isMaximized
                ? viewModel.Strings.RestoreWindowTooltip
                : viewModel.Strings.MaximizeWindowTooltip;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is MainViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateWindowStatePresentation();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.IsSearchVisible) && viewModel.IsSearchVisible)
        {
            Dispatcher.BeginInvoke(() =>
            {
                SearchQueryBox.Focus();
                SearchQueryBox.SelectAll();
                Keyboard.Focus(SearchQueryBox);
            });
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.IsUpdatesVisible) && viewModel.IsUpdatesVisible)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdatesList.Focus();
                Keyboard.Focus(UpdatesList);
            });
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }
}
