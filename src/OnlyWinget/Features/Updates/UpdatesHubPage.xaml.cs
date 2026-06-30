using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesHubPage : Page
{
    public UpdatesHubPage()
    {
        InitializeComponent();
        WingetTab.Header = TextResources.Get("Updates_WingetMode");
        WindowsTab.Header = TextResources.Get("Updates_WindowsMode");
    }
}
