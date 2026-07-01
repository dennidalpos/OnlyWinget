using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Packages;

public sealed partial class PackagesPage : Page
{
    private readonly PresetsPage presets = new();
    private readonly SearchPage search = new();

    public PackagesPage()
    {
        InitializeComponent();
        Scaffold.Title = TextResources.Get("Nav_Packages");
        Scaffold.Subtitle = TextResources.Get("Packages_Subtitle");
        PresetMode.Text = TextResources.Get("Packages_PresetsMode");
        SearchMode.Text = TextResources.Get("Packages_SearchMode");
        ModeSelector.SelectedItem = PresetMode;
        ModeContent.Content = presets;
    }

    private void OnModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) =>
        ModeContent.Content = ReferenceEquals(sender.SelectedItem, SearchMode) ? search : presets;
}
