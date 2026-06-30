using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Packages;

public sealed partial class PackagesPage : Page
{
    public PackagesPage()
    {
        InitializeComponent();
        PresetTab.Header = TextResources.Get("Packages_PresetsMode");
        SearchTab.Header = TextResources.Get("Packages_SearchMode");
    }
}
