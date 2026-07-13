using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Settings;

public sealed partial class SettingsPage : Page
{
    private bool isInitializing;

    public SettingsPage()
    {
        isInitializing = true;
        InitializeComponent();
        ViewModel = new(App.UiServices.Settings);
        isInitializing = false;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public SettingsViewModel ViewModel { get; }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (isInitializing)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                using var cancellation = new CancellationTokenSource();
                await ViewModel.SaveAsync(cancellation.Token);
            }
            catch (Exception exception)
            {
                AppDiagnostics.WriteException("SettingsPage.SaveAsync", exception);
            }
        });
    }

    private async void OnReset(object sender, RoutedEventArgs args)
    {
        if (!await App.UiServices.Confirmation.ConfirmAsync(
                XamlRoot,
                "Settings_Reset",
                "Settings_ResetConfirmation"))
        {
            return;
        }

        isInitializing = true;
        try
        {
            using var cancellation = new CancellationTokenSource();
            await ViewModel.ResetAsync(cancellation.Token);
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("SettingsPage.ResetAsync", exception);
        }
        finally
        {
            isInitializing = false;
        }
    }
}
