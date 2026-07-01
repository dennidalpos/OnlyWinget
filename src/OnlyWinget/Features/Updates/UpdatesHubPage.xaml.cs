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
        ModeSelector.SelectedItem = WingetMode;
    }

    private void OnModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var showWindows = ReferenceEquals(sender.SelectedItem, WindowsMode);
        WindowsWorkflow.Visibility = showWindows ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        WingetWorkflow.Visibility = showWindows ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
