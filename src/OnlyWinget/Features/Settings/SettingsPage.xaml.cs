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
        ApplyText();
        isInitializing = false;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public SettingsViewModel ViewModel { get; }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Settings_Title");
        Scaffold.Subtitle = TextResources.Get("Settings_Subtitle");
        AppearanceGroupHeader.Text = TextResources.Get("Settings_Group_Appearance");
        BehaviorGroupHeader.Text = TextResources.Get("Settings_Group_Behavior");
        MaintenanceGroupHeader.Text = TextResources.Get("Settings_Group_Maintenance");
        LanguageTitle.Text = TextResources.Get("Settings_Language");
        LanguageDescription.Text = TextResources.Get("Settings_LanguageDescription");
        ThemeTitle.Text = TextResources.Get("Settings_Theme");
        ThemeDescription.Text = TextResources.Get("Settings_ThemeDescription");
        ConfirmDestructiveText.Text = TextResources.Get("Settings_ConfirmDestructive");
        DiagnosticsText.Text = TextResources.Get("Settings_Diagnostics");
        InstallBehaviorText.Text = TextResources.Get("Settings_InstallBehavior");
        ResetTitle.Text = TextResources.Get("Settings_Reset");
        ResetDescription.Text = TextResources.Get("Settings_ResetDescription");
        ResetButton.Content = TextResources.Get("Settings_ResetAction");
    }

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
                await ViewModel.SaveAsync(CancellationToken.None);
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
            await ViewModel.ResetAsync(CancellationToken.None);
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
