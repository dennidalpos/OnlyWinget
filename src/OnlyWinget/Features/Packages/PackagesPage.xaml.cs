using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Presentation;

namespace OnlyWinget.Features.Packages;

public sealed partial class PackagesPage : Page, IPendingNavigationGuard
{
    private SelectorBarItem? lastSelectedItem;
    private bool isRestoringSelection;

    public PackagesPage()
    {
        InitializeComponent();
        lastSelectedItem = PresetMode;
    }

    public Task<bool> ConfirmNavigationAsync() => PresetWorkflow.ConfirmNavigationAsync();

    private async void OnModeSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (isRestoringSelection)
        {
            return;
        }

        if (lastSelectedItem == PresetMode && sender.SelectedItem == SearchMode &&
            !await PresetWorkflow.ConfirmNavigationAsync())
        {
            isRestoringSelection = true;
            sender.SelectedItem = lastSelectedItem;
            isRestoringSelection = false;
            return;
        }

        lastSelectedItem = sender.SelectedItem;
    }
}
