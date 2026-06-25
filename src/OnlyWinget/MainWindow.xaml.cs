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
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(PresetsPage));
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
}
