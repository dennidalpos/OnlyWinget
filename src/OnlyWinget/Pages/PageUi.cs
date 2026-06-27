using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Pages;

internal static class PageUi
{
    public static void ApplySelectionHeader(CheckBox checkBox, SelectionHeaderState state)
    {
        checkBox.IsThreeState = true;
        checkBox.IsChecked = state switch
        {
            SelectionHeaderState.Checked => true,
            SelectionHeaderState.Mixed => null,
            _ => false
        };
    }

    public static void ApplyLoading(ProgressRing progressRing, bool isLoading)
    {
        progressRing.IsActive = isLoading;
        progressRing.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    public static void ApplyStatus(TextBlock textBlock, string? error, string emptyText, bool hasRows)
    {
        textBlock.Text = error ?? (hasRows ? string.Empty : emptyText);
    }

    public static void SetEnabled(Control control, IReadOnlyDictionary<string, PresentationCommand> commands, string id)
    {
        if (commands.TryGetValue(id, out var command))
        {
            control.IsEnabled = command.IsEnabled;
        }
    }

    public static void SetVisible(FrameworkElement element, bool isVisible)
    {
        element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public static async Task RunWorkflowAsync(Func<Task> action)
    {
        await action();
    }

    public static void RefreshOnUiThread(Page page, Action refresh)
    {
        if (page.DispatcherQueue.HasThreadAccess)
        {
            refresh();
            return;
        }

        _ = page.DispatcherQueue.TryEnqueue(() => refresh());
    }

    public static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
