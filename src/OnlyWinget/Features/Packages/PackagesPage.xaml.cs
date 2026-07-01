using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Packages;

public sealed partial class PackagesPage : Page
{
    public PackagesPage()
    {
        InitializeComponent();
        Scaffold.Title = TextResources.Get("Nav_Packages");
        Scaffold.Subtitle = TextResources.Get("Packages_Subtitle");
        PresetMode.Text = TextResources.Get("Packages_PresetsMode");
        SearchMode.Text = TextResources.Get("Packages_SearchMode");
        ModeSelector.SelectedItem = PresetMode;
    }

    private void OnModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var showSearch = ReferenceEquals(sender.SelectedItem, SearchMode);
        SearchWorkflow.Visibility = showSearch ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        PresetWorkflow.Visibility = showSearch ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
