using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Presentation;

namespace OnlyWinget.Features.Packages;

public sealed partial class PackagesPage : Page, IPendingNavigationGuard
{
    private SelectorBarItem? lastSelectedItem;
    private bool isRestoringSelection;
    private bool isChangingMode;

    public PackagesPage()
    {
        InitializeComponent();
        lastSelectedItem = PresetMode;
    }

    public Task<bool> ConfirmNavigationAsync() => PresetWorkflow.ConfirmNavigationAsync();

    private async void OnModeSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (isRestoringSelection || isChangingMode)
        {
            return;
        }

        isChangingMode = true;
        try
        {
            if (lastSelectedItem == PresetMode && sender.SelectedItem == SearchMode &&
                !await PresetWorkflow.ConfirmNavigationAsync())
            {
                isRestoringSelection = true;
                sender.SelectedItem = lastSelectedItem;
                return;
            }

            lastSelectedItem = sender.SelectedItem;
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("PackagesPage.OnModeSelectionChanged", exception);
            isRestoringSelection = true;
            sender.SelectedItem = lastSelectedItem;
        }
        finally
        {
            isRestoringSelection = false;
            isChangingMode = false;
        }
    }
}
