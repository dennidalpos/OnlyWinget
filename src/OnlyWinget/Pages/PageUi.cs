using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Pages;

internal static class PageUi
{
    public static void RouteVerticalMouseWheel(ScrollViewer scroller)
    {
        scroller.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler((_, args) =>
            {
                var properties = args.GetCurrentPoint(scroller).Properties;
                if (properties.IsHorizontalMouseWheel || properties.MouseWheelDelta == 0 || scroller.ScrollableHeight <= 0)
                {
                    return;
                }

                var targetOffset = Math.Clamp(
                    scroller.VerticalOffset - properties.MouseWheelDelta,
                    0,
                    scroller.ScrollableHeight);
                if (Math.Abs(targetOffset - scroller.VerticalOffset) < double.Epsilon)
                {
                    return;
                }

                scroller.ChangeView(null, targetOffset, null, disableAnimation: true);
                args.Handled = true;
            }),
            handledEventsToo: true);
    }

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

    public static void SynchronizeItems<T, TKey>(
        ObservableCollection<T> target,
        IEnumerable<T> items,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        var desired = items.ToArray();
        var desiredKeys = desired.Select(keySelector).ToHashSet();

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredKeys.Contains(keySelector(target[index])))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Length; index++)
        {
            var desiredItem = desired[index];
            var desiredKey = keySelector(desiredItem);
            var currentIndex = FindIndex(target, desiredKey, keySelector, index);

            if (currentIndex < 0)
            {
                target.Insert(index, desiredItem);
                continue;
            }

            if (currentIndex != index)
            {
                target.Move(currentIndex, index);
            }

            if (!EqualityComparer<T>.Default.Equals(target[index], desiredItem))
            {
                target[index] = desiredItem;
            }
        }
    }

    private static int FindIndex<T, TKey>(
        ObservableCollection<T> items,
        TKey key,
        Func<T, TKey> keySelector,
        int startIndex)
        where TKey : notnull
    {
        for (var index = startIndex; index < items.Count; index++)
        {
            if (EqualityComparer<TKey>.Default.Equals(keySelector(items[index]), key))
            {
                return index;
            }
        }

        return -1;
    }
}
