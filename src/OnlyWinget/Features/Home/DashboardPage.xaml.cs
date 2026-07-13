using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Home;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        MetricRepeater.ItemsSource = viewModel.Metrics;
        ActivityList.ItemsSource = viewModel.RecentActivity;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.Activate();
        PageState.Present(viewModel.PageState);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => viewModel.Deactivate();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DashboardViewModel.PageState))
        {
            PageState.Present(viewModel.PageState);
        }
    }

    private void OnOpenPackages(object sender, RoutedEventArgs args) => App.Navigate("packages");
    private void OnOpenUpdates(object sender, RoutedEventArgs args) => App.Navigate("updates");
    private void OnViewAllActivity(object sender, RoutedEventArgs args) => App.Navigate("activity");

    public static Microsoft.UI.Xaml.Media.Brush GetThemeBrush(string key)
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var brush) && brush is Microsoft.UI.Xaml.Media.Brush b
            ? b
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
