using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.System;

namespace OnlyWinget.Controls;

public sealed partial class LogViewerDialog : ContentDialog
{
    private readonly ObservableCollection<AppLogEntry> logEntries = new();
    private CancellationTokenSource? exportCancellation;
    private bool isInitialized;

    public LogViewerDialog()
    {
        InitializeComponent();
        isInitialized = true;
        if (App.XamlRoot is not null)
        {
            XamlRoot = App.XamlRoot;
        }
        LogListView.ItemsSource = logEntries;
        Closed += (_, _) => exportCancellation?.Cancel();
        RefreshLogs();
    }

    public static string GetLevelLabel(AppLogLevel level) => TextResources.Get($"Logs_Level_{level}");

    private void RefreshLogs()
    {
        if (!isInitialized) return;
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

        StatusFooter.Text = AppDiagnostics.LastError is { } error
            ? string.Format(TextResources.Get("Logs_StorageError"), error)
            : string.Format(TextResources.Get("Logs_Count"), logEntries.Count);
    }

    private void OnFilterChanged(object sender, object e)
    {
        RefreshLogs();
    }

    private void OnClearClicked(object sender, RoutedEventArgs e) => ClearConfirmation.IsOpen = true;

    private void OnCancelClearClicked(object sender, RoutedEventArgs e) => ClearConfirmation.IsOpen = false;

    private void OnConfirmClearClicked(object sender, RoutedEventArgs e)
    {
        var cleared = AppDiagnostics.ClearLogs();
        ClearConfirmation.IsOpen = !cleared;
        RefreshLogs();
        if (cleared) StatusFooter.Text = TextResources.Get("Logs_Cleared");
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            App.UiServices.Clipboard.CopyText(FormatDisplayedLogs());
            StatusFooter.Text = TextResources.Get("Logs_Copied");
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            AppDiagnostics.WriteException("LogViewerDialog.OnCopyClicked", exception);
            StatusFooter.Text = string.Format(TextResources.Get("Logs_CopyFailed"), exception.Message);
        }
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        if (exportCancellation is not null) return;
        using var cancellation = new CancellationTokenSource();
        exportCancellation = cancellation;
        try
        {
            var saved = await App.UiServices.FilePicker.PickAndWriteTextAsync(App.WindowId,
                $"OnlyWinget-Logs-{DateTime.Now:yyyyMMdd-HHmmss}", ".log", "FileType_Log", FormatDisplayedLogs(), cancellation.Token);
            StatusFooter.Text = TextResources.Get(saved ? "Logs_Exported" : "Logs_ExportCancelled");
        }
        catch (OperationCanceledException) { StatusFooter.Text = TextResources.Get("Logs_ExportCancelled"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
            System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            AppDiagnostics.WriteException("LogViewerDialog.OnExportClicked", ex);
            StatusFooter.Text = string.Format(TextResources.Get("Logs_ExportFailed"), ex.Message);
        }
        finally { exportCancellation = null; }
    }

    private string FormatDisplayedLogs()
    {
        var text = new StringBuilder();
        foreach (var entry in logEntries)
        {
            text.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}");
        }
        return text.ToString();
    }
}
