using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;

namespace OnlyWinget.Pages;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged += OnWorkflowChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged -= OnWorkflowChanged;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Dashboard;
        WingetValueText.Text = state.IsWingetAvailable switch
        {
            true => TextResources.Get("Dashboard_Winget_Available"),
            false => TextResources.Get("Dashboard_Winget_Unavailable"),
            _ => TextResources.Get("Dashboard_Winget_Unknown")
        };
        PresetValueText.Text = state.PresetCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        SearchValueText.Text = state.SearchResultCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        UpdateValueText.Text = state.UpdateCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        SourceValueText.Text = state.SourceCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        WideWingetValueText.Text = WingetValueText.Text;
        WidePresetValueText.Text = PresetValueText.Text;
        WideSearchValueText.Text = SearchValueText.Text;
        WideUpdateValueText.Text = UpdateValueText.Text;
        WideSourceValueText.Text = SourceValueText.Text;
        ActivityList.ItemsSource = state.RecentActivity;
        StatusText.Text = state.Error ?? (state.RecentActivity.Count == 0 ? TextResources.Get("Empty_Activity") : string.Empty);
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Dashboard_Title");
        SummaryText.Text = "OnlyWinget";
        WingetLabelText.Text = TextResources.Get("Dashboard_Winget");
        PresetLabelText.Text = TextResources.Get("Dashboard_Presets");
        SearchLabelText.Text = TextResources.Get("Dashboard_SearchResults");
        UpdateLabelText.Text = TextResources.Get("Dashboard_Updates");
        SourceLabelText.Text = TextResources.Get("Dashboard_Sources");
        WideWingetLabelText.Text = WingetLabelText.Text;
        WidePresetLabelText.Text = PresetLabelText.Text;
        WideSearchLabelText.Text = SearchLabelText.Text;
        WideUpdateLabelText.Text = UpdateLabelText.Text;
        WideSourceLabelText.Text = SourceLabelText.Text;
        RecentActivityText.Text = TextResources.Get("Dashboard_RecentActivity");
    }
}
