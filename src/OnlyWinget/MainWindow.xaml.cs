using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Pages;

namespace OnlyWinget;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Type> pages = new(StringComparer.Ordinal)
    {
        ["presets"] = typeof(PresetsPage),
        ["search"] = typeof(SearchPage),
        ["updates"] = typeof(UpdatesPage),
        ["activity"] = typeof(ActivityPage)
    };

    public MainWindow()
    {
        InitializeComponent();
        RootNavigation.Loaded += OnLoaded;
        ApplyText();
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(PresetsPage));
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        RootNavigation.Loaded -= OnLoaded;
        await App.Workflow.LoadWorkspaceAsync(CancellationToken.None);
        App.NotifyWorkflowChanged();
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item ||
            item.Tag is not string tag ||
            !pages.TryGetValue(tag, out var pageType))
        {
            return;
        }

        ContentFrame.Navigate(pageType);
    }

    private void ApplyText()
    {
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            item.Content = item.Tag switch
            {
                "presets" => TextResources.Get("Nav_Presets"),
                "search" => TextResources.Get("Nav_Search"),
                "updates" => TextResources.Get("Nav_Updates"),
                "activity" => TextResources.Get("Nav_Activity"),
                _ => item.Content
            };
        }
    }
}
