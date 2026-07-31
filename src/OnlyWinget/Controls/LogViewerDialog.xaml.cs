using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OnlyWinget.Controls;

public sealed partial class LogViewerDialog : ContentDialog
{
    private readonly ObservableCollection<AppLogEntry> logEntries = new();

    public LogViewerDialog()
    {
        InitializeComponent();
        if (App.XamlRoot is not null)
        {
            XamlRoot = App.XamlRoot;
        }
        LogListView.ItemsSource = logEntries;
        RefreshLogs();
    }

    public static Microsoft.UI.Xaml.Media.Brush GetBadgeBackground(AppLogLevel level) => level switch
    {
        AppLogLevel.Error => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 216, 59, 1)),
        AppLogLevel.Warning => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 200, 140, 0)),
        AppLogLevel.Information => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 212)),
        _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(30, 128, 128, 128))
    };

    public static Microsoft.UI.Xaml.Media.Brush GetBadgeForeground(AppLogLevel level) => level switch
    {
        AppLogLevel.Error => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 216, 59, 1)),
        AppLogLevel.Warning => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 180, 125, 0)),
        AppLogLevel.Information => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212)),
        _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(230, 140, 140, 140))
    };

    private void RefreshLogs()
    {
        logEntries.Clear();
        AppLogLevel? minLevel = LevelFilterCombo.SelectedIndex switch
        {
            1 => AppLogLevel.Information,
            2 => AppLogLevel.Warning,
            3 => AppLogLevel.Error,
            _ => null
        };

        var text = SearchBox.Text?.Trim();
        var logs = AppDiagnostics.GetRecentLogs(minLevel, text);
        foreach (var entry in logs)
        {
            logEntries.Add(entry);
        }

        StatusFooter.Text = $"Showing {logEntries.Count} log entries";
    }

    private void OnFilterChanged(object sender, object e)
    {
        RefreshLogs();
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        AppDiagnostics.ClearLogs();
        RefreshLogs();
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var entry in logEntries)
        {
            sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}");
        }

        var package = new DataPackage();
        package.SetText(sb.ToString());
        Clipboard.SetContent(package);
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Log File", new List<string> { ".log", ".txt" });
            savePicker.SuggestedFileName = $"OnlyWinget-Logs-{DateTime.Now:yyyyMMdd-HHmmss}";

            var windowHandle = App.WindowHandle;
            if (windowHandle != 0)
            {
                InitializeWithWindow.Initialize(savePicker, windowHandle);
            }

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var sb = new StringBuilder();
                foreach (var entry in logEntries)
                {
                    sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}");
                }
                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.WriteException("LogViewerDialog.OnExportClicked", ex);
        }
    }
}
