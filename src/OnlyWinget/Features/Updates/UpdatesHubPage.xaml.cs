using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesHubPage : Page
{
    public UpdatesHubPage()
    {
        InitializeComponent();
        Scaffold.Title = TextResources.Get("Nav_Updates");
        Scaffold.Subtitle = TextResources.Get("Updates_Subtitle");
        WingetMode.Text = TextResources.Get("Updates_WingetMode");
        WindowsMode.Text = TextResources.Get("Updates_WindowsMode");
    }
}
