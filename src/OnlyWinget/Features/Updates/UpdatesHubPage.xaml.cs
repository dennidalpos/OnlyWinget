using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesHubPage : Page
{
    private readonly UpdatesPage winget = new();
    private readonly WindowsUpdatePage windows = new();

    public UpdatesHubPage()
    {
        InitializeComponent();
        Scaffold.Title = TextResources.Get("Nav_Updates");
        Scaffold.Subtitle = TextResources.Get("Updates_Subtitle");
        WingetMode.Text = TextResources.Get("Updates_WingetMode");
        WindowsMode.Text = TextResources.Get("Updates_WindowsMode");
        ModeSelector.SelectedItem = WingetMode;
        ModeContent.Content = winget;
    }

    private void OnModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) =>
        ModeContent.Content = ReferenceEquals(sender.SelectedItem, WindowsMode) ? windows : winget;
}
