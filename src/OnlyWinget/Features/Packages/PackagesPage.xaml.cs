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
    }
}
